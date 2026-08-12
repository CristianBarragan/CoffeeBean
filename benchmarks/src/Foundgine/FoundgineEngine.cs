using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
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

    public FoundgineEngine(
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
        SemanticRequest request,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var graph = new SemanticRequestResolver(_model).Resolve(request);
        var authorized = new SemanticAuthorizer(_authorizationPolicy).Authorize(graph);
        var plan = _planner.Plan(authorized);
        var cacheKey = _cacheNamespace + ":" + ExecutionPlanFingerprint.Create(plan);
        if (!_planCache.TryGet(cacheKey, out var providerPlan))
        {
            providerPlan = _compiler.Compile(plan);
            _planCache.Set(cacheKey, providerPlan);
        }

        return _provider.ExecuteAsync(
            providerPlan,
            context ?? new ExecutionContext(),
            cancellationToken);
    }
}
