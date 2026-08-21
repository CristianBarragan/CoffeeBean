using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics.Security;
using Xunit;

namespace Foundgine.Planning.Tests.Security;

public sealed class SecurityContractClosureTests
{
    [Fact]
    public void Capability_invariants_are_additive_to_plan_obligations()
    {
        var node = new SemanticPlanNode(
            1, ExecutionOperation.Scan, new EntityId(1), [new FieldId(2)], null, null, []);

        var plan = SecurityInvariantPlanRequirements.Attach(
            new SemanticPlan(node),
            [SecurityInvariantIds.TenantIsolation]);

        Assert.Contains(SecurityInvariantIds.TenantIsolation, plan.RequiredSecurityInvariants!);
        Assert.Contains(SecurityInvariantIds.AuthorizationRequired, plan.RequiredSecurityInvariants!);
        Assert.Contains(SecurityInvariantIds.ParameterizedValues, plan.RequiredSecurityInvariants!);
    }

    [Fact]
    public void Unknown_capability_invariant_is_rejected_before_planning()
    {
        var node = new SemanticPlanNode(
            1, ExecutionOperation.Scan, new EntityId(1), [], null, null, []);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityInvariantPlanRequirements.Attach(
                new SemanticPlan(node),
                ["security.not-real"]));

        Assert.Contains("security.not-real", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Proofless_provider_plan_cannot_cross_execution_boundary()
    {
        var plan = new UnprovedPlan();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityInvariantExecutionGate.EnsureExecutable(plan));

        Assert.Contains("no security proof", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unsatisfied_provider_proof_cannot_cross_execution_boundary()
    {
        var proof = SecurityInvariantProof.Create(
            "test",
            [SecurityInvariantIds.AuthorizationRequired],
            []);
        var plan = new UnprovedPlan { SecurityProof = proof };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityInvariantExecutionGate.EnsureExecutable(plan));

        Assert.Contains(SecurityInvariantIds.AuthorizationRequired, exception.Message, StringComparison.Ordinal);
    }

    private sealed record UnprovedPlan() : ProviderPlan("test");
}
