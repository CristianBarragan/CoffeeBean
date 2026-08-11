using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine;

/// <summary>
/// Small application-facing facade over the Foundgine semantic execution pipeline.
/// Adapters build a SemanticRequest; applications normally do not need to know
/// about resolution, planning, or provider compilation.
/// </summary>
public sealed class FoundgineEngine
{
    private readonly SemanticModel _model;
    private readonly ISemanticAuthorizationPolicy _authorizationPolicy;
    private readonly IPlanner _planner;
    private readonly IProviderPlanCompiler _compiler;
    private readonly IExecutionProvider _provider;

    public FoundgineEngine(
        SemanticModel model,
        ISemanticAuthorizationPolicy authorizationPolicy,
        IPlanner planner,
        IProviderPlanCompiler compiler,
        IExecutionProvider provider)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _authorizationPolicy = authorizationPolicy ?? throw new ArgumentNullException(nameof(authorizationPolicy));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public Task<ExecutionResult> ExecuteAsync(
        SemanticRequest request,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var graph = new SemanticRequestResolver(_model).Resolve(request);
        var authorized = new SemanticAuthorizer(_authorizationPolicy).Authorize(graph);
        var plan = _planner.Plan(authorized);
        var providerPlan = _compiler.Compile(plan);

        return _provider.ExecuteAsync(
            providerPlan,
            context ?? new ExecutionContext(),
            cancellationToken);
    }
}
