namespace Foundgine.Planning;

/// <summary>Auditable result of applying one rewrite rule.</summary>
public sealed record PlanRewriteRuleResult(
    string RuleName,
    SemanticPlan Before,
    SemanticPlan After,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> SecurityObligations,
    double CostImpact,
    SecurityPreservationProof SecurityProof,
    SemanticEquivalenceProof SemanticProof)
{
    public bool IsSatisfied => SecurityProof.IsSatisfied && SemanticProof.IsSatisfied;
}
