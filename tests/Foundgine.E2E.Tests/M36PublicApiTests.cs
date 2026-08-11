using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>M36: the application-facing facade must not require callers to manually
/// orchestrate resolution, authorization, planning, or provider compilation.</summary>
public sealed class M36PublicApiTests
{
    [Fact]
    public async Task Public_facade_executes_the_core_pipeline()
    {
        var model = Banking.BankingSemanticModel.Build();
        var policy = new AllowAllSemanticAuthorizationPolicy();
        var compiler = new TestProviderPlanCompiler();
        var provider = new TestExecutionProvider();

        var engine = new Foundgine.FoundgineEngine(
            model,
            policy,
            new Planner(),
            compiler,
            provider);

        var request = new SemanticRequest(
            Banking.BankingSemanticModel.Customer,
            [new SemanticSelection(new FieldId(2), null, [])]);

        var result = await engine.ExecuteAsync(request);

        Assert.Single(result.Rows);
        Assert.Equal(1, compiler.CompiledCount);
        Assert.Equal(1, provider.ExecutionCount);
        Assert.Equal("Alice", result.Rows[0].Values["Name"]);
    }

    private sealed class TestProviderPlanCompiler : IProviderPlanCompiler
    {
        public int CompiledCount { get; private set; }

        public ProviderPlan Compile(ExecutionPlan plan)
        {
            CompiledCount++;
            return new TestPlan();
        }
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
                [new ExecutionRow(new Dictionary<string, object?>
                {
                    ["Name"] = "Alice"
                })]));
        }
    }

    private sealed record TestPlan() : ProviderPlan("test");
}
