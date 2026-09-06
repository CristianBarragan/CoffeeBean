namespace Foundgine.Core.Semantic.Planning.Tests;

public sealed class PlanOptimizationProofTests
{
    [Fact]
    public void OptimizationProof_RequiresAllPreservationDimensions()
    {
        var proof = new PlanOptimizationProof(
            "test-rule",
            true,
            true,
            true,
            1d,
            0.5d);

        Assert.True(proof.IsSatisfied);
    }

    [Fact]
    public void OptimizationProof_RejectsSemanticLoss()
    {
        var proof = new PlanOptimizationProof(
            "test-rule",
            false,
            true,
            true,
            10d,
            0d);

        Assert.False(proof.IsSatisfied);
    }

    [Fact]
    public void OptimizationProof_RejectsAuthorizationBindingLoss()
    {
        var proof = new PlanOptimizationProof(
            "test-rule",
            true,
            true,
            false,
            10d,
            0d);

        Assert.False(proof.IsSatisfied);
    }
}