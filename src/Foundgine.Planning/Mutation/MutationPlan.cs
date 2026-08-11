namespace Foundgine.Planning.Mutation;

public sealed record MutationPlan(
    IReadOnlyList<MutationOperation> Operations);
