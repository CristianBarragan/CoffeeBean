using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Execution.Mutation;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Security;
using Foundgine.Semantics.Security.Execution;
using Foundgine.Semantics.Security.Warrants;
using Foundgine.Semantics.Mutation;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine;

public sealed class FoundgineMutationEngine : IFoundgineMutations
{
    private readonly IMutationSchema _schema;
    private readonly ISemanticAuthorizationPolicy _policy;
    private readonly IMutationBatchExecutionProvider _provider;
    private readonly SemanticCapabilityContract _securityContract;
    private readonly ISecurityWarrantKeyResolver? _warrantKeyResolver;
    private readonly string? _expectedWarrantIssuer;
    private readonly ISecurityWarrantReplayStore? _warrantReplayStore;
    private readonly SecurityResourceLimits _securityResourceLimits;

    public FoundgineMutationEngine(
        IMutationSchema schema,
        ISemanticAuthorizationPolicy policy,
        IMutationBatchExecutionProvider provider,
        SemanticModel? model = null,
        ISecurityWarrantKeyResolver? warrantKeyResolver = null,
        string? expectedWarrantIssuer = null,
        ISecurityWarrantReplayStore? warrantReplayStore = null,
        SecurityResourceLimits? securityResourceLimits = null)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _securityContract = model is null
            ? new SemanticCapabilityContract(1, [])
            : SemanticCapabilityContractDiscovery.Describe(model, policy);
        SecurityInvariantContractValidator.EnsureContractValid(_securityContract);
        _warrantKeyResolver = warrantKeyResolver;
        _expectedWarrantIssuer = expectedWarrantIssuer;
        _warrantReplayStore = warrantReplayStore;
        _securityResourceLimits = securityResourceLimits ?? new SecurityResourceLimits();
        _securityResourceLimits.Validate();
    }

    public MutationDryRunResult DryRun(SemanticMutationRequest request)
    {
        var plan = AuthorizeAndPlan(request, consumeReplay: false);
        return Describe(plan);
    }

    public MutationPlanApproval Approve(SemanticMutationRequest request, string approvedBy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        var dryRun = DryRun(request);
        return new MutationPlanApproval(
            request,
            Guid.NewGuid().ToString("N"),
            dryRun.PlanFingerprint,
            approvedBy,
            DateTimeOffset.UtcNow);
    }

    public Task<MutationExecutionResult> ExecuteApprovedAsync(
        MutationPlanApproval approval,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = AuthorizeAndPlan(approval.Request, consumeReplay: true);
        var fingerprint = Fingerprint(plan);
        if (!string.Equals(fingerprint, approval.PlanFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The approved mutation plan changed after approval. Re-run dry-run and obtain a new approval.");

        var ir = new SemanticMutationExecutionLowerer(_schema).Lower(plan);
        var executionContext = context ?? new ExecutionContext();
        executionContext.EnsureWithinDeadline();
        var started = DateTimeOffset.UtcNow;
        using var deadlineCts = executionContext.CreateDeadlineCancellationSource(cancellationToken);
        var result = _provider.ExecuteBatch(ir, executionContext, deadlineCts.Token);
        executionContext.EnsureWithinDeadline();
        var resultFingerprint = FingerprintResult(result);
        _ = started;
        return Task.FromResult(new MutationExecutionResult(
            result,
            fingerprint,
            resultFingerprint,
            approval.ApprovalId,
            approval.ApprovedBy));
    }


    private void ValidateWarrant(SemanticMutationRequest request, bool consumeReplay)
    {
        var security = request.Security;
        if (security is null)
            return;

        if (_warrantKeyResolver is null)
            throw new InvalidOperationException("A security warrant was supplied, but no warrant key resolver is configured.");

        SecurityWarrantVerifier.Verify(
            security.Warrant,
            _warrantKeyResolver,
            DateTimeOffset.UtcNow,
            _expectedWarrantIssuer,
            security.Audience);

        foreach (var operation in request.Graph.Operations)
        {
            var capability = _securityContract.Capabilities.FirstOrDefault(c =>
                c.TargetEntityId == operation.Entity &&
                string.Equals(c.Operation, operation.Kind.ToString().ToLowerInvariant(), StringComparison.Ordinal));

            if (capability is null)
                throw new UnauthorizedAccessException(
                    $"No security capability contract exists for entity '{operation.Entity}' and operation '{operation.Kind}'.");

            if (!SecurityWarrantAuthorization.Allows(
                    security.Warrant,
                    security.Subject,
                    security.Audience,
                    capability.Id,
                    capability.Operation,
                    security.Tenant,
                    security.ResourceScope))
            {
                throw new UnauthorizedAccessException(
                    $"Security warrant does not authorize capability '{capability.Id}' for subject '{security.Subject}'.");
            }
        }

        if (consumeReplay)
        {
            if (_warrantReplayStore is null)
                throw new InvalidOperationException("Executing a warrant-backed mutation requires a warrant replay store.");

            SecurityWarrantReplayGuard.Consume(
                security.Warrant,
                _warrantReplayStore,
                DateTimeOffset.UtcNow);
        }
    }
    private SemanticMutationPlan AuthorizeAndPlan(SemanticMutationRequest request, bool consumeReplay)
    {
        ArgumentNullException.ThrowIfNull(request);
        MutationSecurityResourceLimitValidator.Validate(request, _securityResourceLimits);
        ValidateWarrant(request, consumeReplay);
        var semanticPlan = new SemanticMutationPlanner().Plan(request.Graph);
        var batch = new MutationPlanner(_schema).Plan(request.Graph);
        var authorizedBatch = new MutationAuthorizer(_schema, _policy).Authorize(batch);

        // The semantic plan is retained as the canonical representation while the
        // provider-neutral batch is used to validate schema and authorization.
        // Reconstructing from the authorized batch is intentionally avoided: that
        // would make physical mappings the semantic source of truth.
        _ = authorizedBatch;
        return semanticPlan;
    }

    private MutationDryRunResult Describe(SemanticMutationPlan plan) =>
        new(
            Fingerprint(plan),
            plan.Operations.Select((x, i) => new MutationPlanOperation(
                i,
                _schema.GetEntity(x.Entity).Name,
                x.Kind.ToString(),
                x.Fields.Select(f => f.Field.Value.ToString()).ToArray(),
                x.ReturnFields.Select(f => f.Value.ToString()).ToArray())).ToArray(),
            plan.Operations.SelectMany(x => x.Effects).Select(FormatEffect).ToArray());

    private string FormatEffect(SemanticMutationEffect effect) =>
        $"{effect.Kind}:{_schema.GetEntity(effect.Entity).Name}" +
        (effect.Field is { } field ? $".{field.Value}" : string.Empty);

    private static string Fingerprint(SemanticMutationPlan plan)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            version = "mutation-plan-v1",
            operations = plan.Operations,
            dependencies = plan.Dependencies
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string FingerprintResult(MutationBatchResult result)
    {
        var canonical = JsonSerializer.Serialize(result.Results.Select(x => new
        {
            x.AffectedRows,
            returned = x.ReturnedValues?.OrderBy(p => p.Key.Value)
                .Select(p => new { field = p.Key.Value, value = p.Value })
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
