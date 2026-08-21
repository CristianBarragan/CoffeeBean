using Foundgine.Execution;
using Foundgine.Execution.Security;
using Foundgine.Semantics.Security;
using Xunit;

namespace Foundgine.Security.Tests.Security;

public sealed class ProviderExecutableConformanceGateTests
{
    [Fact]
    public void Gate_rejects_executable_conformance_violation()
    {
        var ir = TestIr(SecurityInvariantIds.AuthorizationRequired);
        var compiler = new HostileCertifiedCompiler();
        var plan = compiler.Compile(ir);

        Assert.Throws<InvalidOperationException>(() =>
            SecurityInvariantProofGate.AttachAndValidate(plan, ir, compiler));
    }

    private static ExecutionIR TestIr(string invariant) =>
        new(
            new ExecutionIRNode(
                1,
                default,
                default,
                [],
                null,
                null,
                [],
                null,
                null),
            [invariant]);

    private sealed class HostileCertifiedCompiler : IProviderPlanCompiler, ISecurityInvariantProviderCompiler, IProviderSecurityConformanceEvaluator
    {
        public IReadOnlyCollection<string> PreservedSecurityInvariants =>
            [SecurityInvariantIds.AuthorizationRequired];

        public ProviderPlan Compile(ExecutionIR ir) => new TestPlan();

        public ProviderSecurityConformanceResult Evaluate(ExecutionIR ir, ProviderPlan plan) =>
            new(
                plan.Provider,
                ir.RequiredSecurityInvariants,
                [],
                ["compiled provider plan lost authorization predicate"]);
    }

    private sealed record TestPlan() : ProviderPlan("hostile-certified");
}
