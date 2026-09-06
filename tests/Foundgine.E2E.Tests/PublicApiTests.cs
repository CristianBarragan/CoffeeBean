using Foundgine.Core.Abstractions;
using Foundgine.Core.Execution;
using Foundgine.Core.Execution.Security;
using Foundgine.Core.Semantic;
using Foundgine.Core.Semantic.Authorization;
using Foundgine.Core.Semantic.Security;
using Foundgine.E2E.Tests.Banking;
using Foundgine.Runtime;
using ExecutionContext = Foundgine.Core.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>
///     : the application-facing facade must not require callers to manually
///     orchestrate resolution, authorization, planning, or provider compilation.
/// </summary>
public sealed class PublicApiTests
{
    [Fact]
    public async Task Public_facade_executes_the_core_pipeline_through_di()
    {
        var model = BankingSemanticModel.Build();
        var policy = new AllowAllSemanticAuthorizationPolicy();
        var compiler = new TestProviderPlanCompiler();
        var provider = new TestExecutionProvider();

        var services = new ServiceCollection();
        services.AddSingleton<IProviderPlanCompiler>(compiler);
        services.AddSingleton<IExecutionProvider>(provider);
        services.AddFoundgine(model, policy);

        var engine = services.BuildServiceProvider().GetRequiredService<IFoundgine>();

        var request = new SemanticRequest(
            BankingSemanticModel.Customer,
            [new SemanticSelection(new FieldId(2), null, [])]);

        var result = await engine.ExecuteAsync(request);

        Assert.Single(result.Rows);
        Assert.Equal(1, compiler.CompiledCount);
        Assert.Equal(1, provider.ExecutionCount);
        Assert.Equal("Alice", result.Rows[0].Values["Name"]);
    }

    private sealed class TestProviderPlanCompiler : IProviderPlanCompiler, ISecurityInvariantProviderCompiler,
        IProviderSecurityConformanceEvaluator
    {
        public int CompiledCount { get; private set; }


        public ProviderPlan Compile(ExecutionIR ir)
        {
            CompiledCount++;
            return new TestPlan();
        }

        public ProviderSecurityConformanceResult Evaluate(ExecutionIR ir, ProviderPlan plan)
        {
            return new ProviderSecurityConformanceResult(
                plan.Provider,
                ir.RequiredSecurityInvariants,
                ir.RequiredSecurityInvariants.Where(PreservedSecurityInvariants.Contains).ToArray(),
                Array.Empty<string>());
        }

        public IReadOnlyCollection<string> PreservedSecurityInvariants =>
            SecurityInvariantRegistry.AllInvariants.Select(x => x.Id).ToArray();
    }

    private sealed class TestExecutionProvider : IExecutionProvider
    {
        public int ExecutionCount { get; private set; }

        public Task<ExecutionResult> ExecuteAsync(
            ProviderPlan plan,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(new ExecutionResult(
            [
                new ExecutionRow(new Dictionary<string, object?>
                {
                    ["Name"] = "Alice"
                })
            ]));
        }
    }

    private sealed record TestPlan() : ProviderPlan("test");
}