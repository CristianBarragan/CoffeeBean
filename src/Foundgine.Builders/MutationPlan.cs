namespace Foundgine.Builders;

/// <summary>Root of a logical, provider-agnostic mutation plan.</summary>
public sealed record MutationPlan(
    IReadOnlyList<MutationOperation> Operations
);
