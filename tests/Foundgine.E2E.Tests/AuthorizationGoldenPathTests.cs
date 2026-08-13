using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>
/// The repository-level authorization proof: capability discovery is
/// descriptive, authorization is applied again to the semantic graph, the
/// conditional predicate survives planning, and the provider receives the
/// predicate rather than a pre-authorized result.
/// </summary>
public sealed class AuthorizationGoldenPathTests
{
    [Fact]
    public void Capability_discovery_does_not_authorize_execution()
    {
        var model = Banking.BankingSemanticModel.Build();
        var policy = new TenantPolicy();
        var compiler = new CapturingCompiler();
        var provider = new CapturingProvider();
        var engine = new FoundgineEngine(
            new FoundgineOptions { Model = model, AuthorizationPolicy = policy },
            compiler,
            provider);

        var capabilities = engine.DescribeCapabilities();
        Assert.Contains(capabilities.Entities, x => x.EntityId == Banking.BankingSemanticModel.Customer);

        // Discovery does not invoke the provider or compile an execution plan.
        // The actual request below still passes through SemanticAuthorizer.
        Assert.Null(compiler.Plan);
    }

    [Fact]
    public async Task Conditional_authorization_survives_semantic_pipeline()
    {
        var policy = new TenantPolicy();
        var compiler = new CapturingCompiler();
        var provider = new CapturingProvider();
        var engine = new FoundgineEngine(
            new FoundgineOptions { Model = Banking.BankingSemanticModel.Build(), AuthorizationPolicy = policy },
            compiler,
            provider);

        var request = new SemanticRequest(
            Banking.BankingSemanticModel.Customer,
            [new SemanticSelection(new FieldId(1), null, [])]);

        await engine.ExecuteAsync(request, new ExecutionContext(
            new Dictionary<string, object?> { ["user.TenantId"] = 7 }));

        Assert.NotNull(compiler.Plan);
        Assert.NotNull(compiler.Plan!.Root.Authorization);
        Assert.Equal(AuthorizationPredicateKind.Equal, compiler.Plan.Root.Authorization!.Kind);
        Assert.NotNull(provider.Context);
        Assert.Equal(7, provider.Context!.EffectiveValues["user.TenantId"]);
        Assert.Equal(1, policy.EntityChecks);
    }

    private sealed class TenantPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public int EntityChecks { get; private set; }

        public override bool CanAccessEntity(EntityId entityId)
        {
            EntityChecks++;
            return true;
        }

        public override AuthorizationPredicate? GetPredicate(
            EntityId entityId,
            AuthorizationOperation operation) =>
            operation == AuthorizationOperation.Read && entityId == Banking.BankingSemanticModel.Customer
                ? AuthorizationPredicate.Equal(
                    AuthorizationPredicate.Member(
                        AuthorizationPredicate.ResourceParameter("resource"), "TenantId"),
                    AuthorizationPredicate.Member(
                        AuthorizationPredicate.ContextParameter("user"), "TenantId"))
                : null;
    }

    private sealed class CapturingCompiler : IProviderPlanCompiler
    {
        public ExecutionPlan? Plan { get; private set; }

        public ProviderPlan Compile(ExecutionPlan plan)
        {
            Plan = plan;
            return new TestPlan();
        }
    }

    private sealed class CapturingProvider : IExecutionProvider
    {
        public ExecutionContext? Context { get; private set; }

        public Task<ExecutionResult> ExecuteAsync(
            ProviderPlan plan,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            return Task.FromResult(new ExecutionResult(Array.Empty<ExecutionRow>()));
        }
    }

    private sealed record TestPlan() : ProviderPlan("test");
}
