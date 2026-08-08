namespace CoffeeBeanery.GraphQL.Core.Foundation.MutationPlan;

/// <summary>Root of a mutation plan: its own pipeline, not part of QueryPlan.</summary>
public sealed record MutationPlan(
    IReadOnlyList<MutationOperation> Operations
);
