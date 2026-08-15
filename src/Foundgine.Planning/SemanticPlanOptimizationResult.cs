namespace Foundgine.Planning;

/// <summary>
/// Result of provider-neutral semantic plan optimization.
/// </summary>
public sealed record SemanticPlanOptimizationResult(
    SemanticPlan Plan,
    IReadOnlyList<string> AppliedRules)
{
    public bool Changed => AppliedRules.Count != 0;
}
