using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.IR;
using Foundgine.Semantics.Resolution;
using System.Text.Json;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine;

/// <summary>
/// Application-facing facade over the Foundgine semantic execution pipeline.
/// Applications normally obtain this through dependency injection.
/// </summary>
public sealed class FoundgineEngine : IFoundgine
{
    private readonly SemanticModel _model;
    private readonly ISemanticAuthorizationPolicy _authorizationPolicy;
    private readonly IPlanner _planner;
    private readonly IPlanOptimizer _planOptimizer;
    private readonly IProviderPlanCompiler _compiler;
    private readonly IExecutionProvider _provider;
    private readonly IProviderPlanCache _planCache;
    private readonly SemanticVersionSet _versions;
    private readonly string _cacheNamespace = Guid.NewGuid().ToString("N");

    internal FoundgineEngine(
        FoundgineOptions options,
        IProviderPlanCompiler compiler,
        IExecutionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _model = options.Model ?? throw new InvalidOperationException(
            "FoundgineOptions.Model must be configured.");
        _authorizationPolicy = options.AuthorizationPolicy ?? throw new InvalidOperationException(
            "FoundgineOptions.AuthorizationPolicy must be configured.");
        _planner = new Planner();
        _planOptimizer = new SemanticPlanOptimizer();
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _planCache = options.PlanCache ?? new MemoryProviderPlanCache();
        _versions = SemanticVersionSet.For(_model);
    }

    /// <summary>
    /// Internal-compatible constructor for adapters/tests that intentionally
    /// provide the orchestration components themselves.
    /// </summary>
    internal FoundgineEngine(
        SemanticModel model,
        ISemanticAuthorizationPolicy authorizationPolicy,
        IPlanner planner,
        IProviderPlanCompiler compiler,
        IExecutionProvider provider,
        IProviderPlanCache? planCache = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _authorizationPolicy = authorizationPolicy ?? throw new ArgumentNullException(nameof(authorizationPolicy));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _planOptimizer = new SemanticPlanOptimizer();
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _planCache = planCache ?? new MemoryProviderPlanCache();
        _versions = SemanticVersionSet.For(_model);
    }

    public SemanticAuthorizationCapabilities DescribeCapabilities() =>
        SemanticAuthorizationCapabilityDiscovery.Describe(_model, _authorizationPolicy);

    public SemanticCapabilityContract DescribeCapabilityContract() =>
        SemanticCapabilityContractDiscovery.Describe(_model, _authorizationPolicy);

    public SemanticVersionSet DescribeVersionSet() => _versions;

    public DryRunResult DryRun(
        SemanticRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var graph = new SemanticRequestResolver(_model).Resolve(request);
        var semanticOperation = SemanticOperationCompiler.Compile(graph);
        var authorizedOperation = new SemanticAuthorizer(_authorizationPolicy).Authorize(semanticOperation);
        var plan = _planOptimizer.Optimize(_planner.Plan(authorizedOperation)).Plan;
        return new DryRunResult(PlanInspector.Inspect(plan));
    }

    public PlanApproval ApprovePlan(SemanticRequest request, string approvedBy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);

        var dryRun = DryRun(request);
        return new PlanApproval(
            request,
            Guid.NewGuid().ToString("N"),
            dryRun.Inspection.PlanFingerprint,
            _versions.SemanticModelVersion,
            _versions.CapabilityContractVersion,
            _versions.CapabilityVersion,
            _versions.IntentVersion,
            _versions.PlanVersion,
            approvedBy,
            DateTimeOffset.UtcNow);
    }

    public Task<ExecutionResult> ExecuteApprovedAsync(
        PlanApproval approval,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);

        if (!string.Equals(approval.SemanticModelVersion, _versions.SemanticModelVersion, StringComparison.Ordinal) ||
            approval.CapabilityContractVersion != _versions.CapabilityContractVersion ||
            approval.CapabilityVersion != _versions.CapabilityVersion ||
            approval.IntentVersion != _versions.IntentVersion ||
            approval.PlanVersion != _versions.PlanVersion)
        {
            throw new InvalidOperationException(
                "The approval was created against an incompatible semantic version set. Re-run dry-run and obtain a new approval.");
        }

        var graph = new SemanticRequestResolver(_model).Resolve(approval.Request);
        var semanticOperation = SemanticOperationCompiler.Compile(graph);
        var authorizedOperation = new SemanticAuthorizer(_authorizationPolicy).Authorize(semanticOperation);
        var plan = _planOptimizer.Optimize(_planner.Plan(authorizedOperation)).Plan;
        var currentFingerprint = SemanticPlanFingerprint.Create(plan);

        if (!string.Equals(currentFingerprint, approval.PlanFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The approved plan no longer matches the current authorized plan. Re-run dry-run and obtain a new approval.");
        }

        var executionIr = ExecutionIRCompiler.Compile(plan);
        var cacheKey = _cacheNamespace + ":" + SemanticPlanFingerprint.CreateShapeKey(plan);
        var providerPlan = _planCache.GetOrAdd(
            cacheKey,
            () => _compiler.Compile(executionIr));

        var executionContext = AttachPaginationContext(plan, context ?? new ExecutionContext());
        return ExecuteAndEnrichEvidenceAsync(
            approval.Request,
            plan,
            providerPlan,
            executionContext,
            cancellationToken,
            approval);
    }

    public Task<ExecutionResult> ExecuteAsync(
        Foundgine.Semantics.Intent.ReadIntent intent,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var request = new Foundgine.Semantics.Intent.ReadIntentCompiler(_model).Compile(intent);
        return ExecuteAsync(request, context, cancellationToken);
    }

    public Task<ExecutionResult> ExecuteAsync(
        SemanticRequest request,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var graph = new SemanticRequestResolver(_model).Resolve(request);
        var semanticOperation = SemanticOperationCompiler.Compile(graph);
        var authorizedOperation = new SemanticAuthorizer(_authorizationPolicy).Authorize(semanticOperation);
        var plan = _planOptimizer.Optimize(_planner.Plan(authorizedOperation)).Plan;
        var executionIr = ExecutionIRCompiler.Compile(plan);
        var cacheKey = _cacheNamespace + ":" + SemanticPlanFingerprint.CreateShapeKey(plan);
        var providerPlan = _planCache.GetOrAdd(
            cacheKey,
            () => _compiler.Compile(executionIr));

        var executionContext = AttachPaginationContext(plan, context ?? new ExecutionContext());

        return ExecuteAndEnrichEvidenceAsync(
            request,
            plan,
            providerPlan,
            executionContext,
            cancellationToken);
    }
    private async Task<ExecutionResult> ExecuteAndEnrichEvidenceAsync(
        SemanticRequest request,
        SemanticPlan plan,
        ProviderPlan providerPlan,
        ExecutionContext context,
        CancellationToken cancellationToken,
        PlanApproval? approval = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var result = await _provider.ExecuteAsync(providerPlan, context, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;
        if (result.Evidence is null)
            return result;

        var intentFingerprint = ExecutionEvidenceFactory.Hash(
            $"intent-v{_versions.IntentVersion}|{JsonSerializer.Serialize(request)}");
        var authorizationFingerprint = ExecutionEvidenceFactory.Hash(
            SemanticPlanFingerprint.Create(plan));
        var evidence = result.Evidence with
        {
            PlanFingerprint = SemanticPlanFingerprint.Create(plan),
            IntentFingerprint = intentFingerprint,
            AuthorizationFingerprint = authorizationFingerprint
        };

        var effects = plan.Root.Operation == ExecutionOperation.Scan
            ? new[] { "read" }
            : new[] { "read", "relationship-traversal" };
        var affectedNodeIds = EnumerateNodes(plan.Root).Select(node => node.Id);
        var receipt = ExecutionReceiptFactory.Create(
            requestId: Guid.NewGuid().ToString("N"),
            evidence,
            ExecutionReceiptFactory.FingerprintResult(result),
            affectedNodeIds,
            effects,
            startedAt,
            completedAt,
            _versions.CapabilityContractVersion,
            _versions.CapabilityVersion,
            _versions.IntentVersion,
            _versions.PlanVersion,
            _versions.SemanticModelVersion,
            approval?.ApprovalId,
            approval?.ApprovedBy,
            approval?.ApprovedAt);

        return result with
        {
            Evidence = evidence,
            Receipt = receipt
        };
    }

    private static IEnumerable<SemanticPlanNode> EnumerateNodes(SemanticPlanNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in EnumerateNodes(child))
            yield return descendant;
    }

    private static ExecutionContext AttachPaginationContext(SemanticPlan plan, ExecutionContext context)
    {
        var options = plan.Root.QueryOptions;
        if (options?.Limit is null && options?.Offset is null)
            return context;

        var values = new Dictionary<string, object?>(context.EffectiveValues, StringComparer.Ordinal);
        if (options.Limit is { } limit)
            values[ExecutionContextKeys.PaginationLimit] = limit;
        if (options.Offset is { } offset)
            values[ExecutionContextKeys.PaginationOffset] = offset;
        values[ExecutionContextKeys.PaginationHasCursor] = options.After is not null;

        return new ExecutionContext(values);
    }

}
