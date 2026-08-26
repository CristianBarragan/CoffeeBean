using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Execution.Mutation;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics;
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
    private readonly ISecurityWarrantDelegationTrustResolver? _warrantDelegationTrustResolver;
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
        SecurityResourceLimits? securityResourceLimits = null,
        ISecurityWarrantDelegationTrustResolver? warrantDelegationTrustResolver = null)
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
        _warrantDelegationTrustResolver = warrantDelegationTrustResolver;
        _warrantReplayStore = warrantReplayStore;
        _securityResourceLimits = securityResourceLimits ?? new SecurityResourceLimits();
        _securityResourceLimits.Validate();
    }

    public MutationDryRunResult DryRun(SemanticMutationRequest request)
    {
        var plan = AuthorizeAndPlan(request);
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

        // Replay consumption is deliberately deferred until the exact approved
        // semantic plan, lowered IR, security contract and execution context have
        // all been validated. A failed approval fingerprint or security check must
        // not burn a warrant that was never executable.
        var plan = AuthorizeAndPlan(approval.Request);
        var fingerprint = Fingerprint(plan);
        if (!string.Equals(fingerprint, approval.PlanFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The approved mutation plan changed after approval. Re-run dry-run and obtain a new approval.");

        var ir = new SemanticMutationExecutionLowerer(_schema).Lower(plan);
        var executionContext = context ?? new ExecutionContext();
        executionContext.EnsureWithinDeadline();
        using var deadlineCts = executionContext.CreateDeadlineCancellationSource(cancellationToken);

        var certificate = MutationExecutionSecurityGate.Certify(
            ir,
            _provider,
            _provider.GetType().FullName ?? _provider.GetType().Name,
            plan.RequiredSecurityInvariants.Where(IsEnginePreservedInvariant));

        // The final security gate must pass before the replay identity is consumed.
        // Replay is then committed immediately before the provider side effect.
        MutationExecutionSecurityGate.EnsureExecutable(ir, _provider, certificate);
        ConsumeWarrantReplay(approval.Request);

        var started = DateTimeOffset.UtcNow;
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


    private void ValidateWarrant(SemanticMutationRequest request)
    {
        var security = request.Security;
        if (security is null)
            return;

        if (_warrantKeyResolver is null)
            throw new InvalidOperationException("A security warrant was supplied, but no warrant key resolver is configured.");

        if (string.IsNullOrWhiteSpace(_expectedWarrantIssuer))
            throw new InvalidOperationException("Warrant-backed mutation execution requires an explicit trusted issuer.");

        SecurityWarrantExecutionTrust.Verify(
            security.Warrant,
            _warrantKeyResolver,
            _expectedWarrantIssuer,
            security.Audience,
            DateTimeOffset.UtcNow,
            security.DelegationChain,
            _warrantDelegationTrustResolver,
            security.Tenant);

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

    }

    private void ConsumeWarrantReplay(SemanticMutationRequest request)
    {
        var security = request.Security;
        if (security is null)
            return;

        if (_warrantReplayStore is null)
            throw new InvalidOperationException("Executing a warrant-backed mutation requires a warrant replay store.");

        SecurityWarrantReplayGuard.Consume(
            security.Warrant,
            _warrantReplayStore,
            DateTimeOffset.UtcNow);
    }

    private SemanticMutationPlan AuthorizeAndPlan(SemanticMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MutationSecurityResourceLimitValidator.Validate(request, _securityResourceLimits);
        ValidateWarrant(request);
        var semanticPlan = new SemanticMutationPlanner().Plan(request.Graph);
        var authorizedPlan = new MutationAuthorizer(_schema, _policy).Authorize(semanticPlan);
        var requiredSecurityInvariants = RequiredSecurityInvariantsFor(authorizedPlan);

        // Authorization is applied to the exact semantic representation that is
        // subsequently lowered into ExecutionMutationIR. No independently planned
        // batch is discarded or allowed to become an alternate execution source.
        return authorizedPlan with
        {
            RequiredSecurityInvariants = requiredSecurityInvariants
        };
    }

    private IReadOnlyList<string> RequiredSecurityInvariantsFor(SemanticMutationPlan plan)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);

        foreach (var operation in plan.Operations)
        {
            var capability = _securityContract.Capabilities.FirstOrDefault(c =>
                c.TargetEntityId == operation.Entity &&
                string.Equals(c.Operation, operation.Kind.ToString().ToLowerInvariant(), StringComparison.Ordinal));

            if (capability is null)
                throw new InvalidOperationException(
                    $"No security capability contract exists for entity '{operation.Entity}' and operation '{operation.Kind}'.");

            foreach (var invariant in capability.EffectiveSecurityInvariants)
                required.Add(invariant);
        }

        if (required.Count == 0)
            throw new InvalidOperationException(
                "A mutation execution cannot proceed without explicit security invariants.");

        return required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static bool IsEnginePreservedInvariant(string id) => id switch
    {
        SecurityInvariantIds.AuthorizationRequired => true,
        SecurityInvariantIds.RuntimeAuthorization => true,
        SecurityInvariantIds.FieldVisibility => true,
        SecurityInvariantIds.RelationshipVisibility => true,
        _ => false
    };

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
            dependencies = plan.Dependencies,
            security = plan.RequiredSecurityInvariants
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
