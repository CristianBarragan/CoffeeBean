using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
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
    private readonly IProviderPlanCompiler _compiler;
    private readonly IExecutionProvider _provider;
    private readonly IProviderPlanCache _planCache;
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
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _planCache = options.PlanCache ?? new MemoryProviderPlanCache();
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
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _planCache = planCache ?? new MemoryProviderPlanCache();
    }

    public SemanticAuthorizationCapabilities DescribeCapabilities() =>
        SemanticAuthorizationCapabilityDiscovery.Describe(_model, _authorizationPolicy);

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
        var plan = _planner.Plan(authorizedOperation);
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
        CancellationToken cancellationToken)
    {
        var result = await _provider.ExecuteAsync(providerPlan, context, cancellationToken);
        if (result.Evidence is null)
            return result;

        var intentFingerprint = ExecutionEvidenceFactory.Hash(
            JsonSerializer.Serialize(request));
        var authorizationFingerprint = ExecutionEvidenceFactory.Hash(
            SemanticPlanFingerprint.Create(plan));

        return result with
        {
            Evidence = result.Evidence with
            {
                IntentFingerprint = intentFingerprint,
                AuthorizationFingerprint = authorizationFingerprint
            }
        };
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
