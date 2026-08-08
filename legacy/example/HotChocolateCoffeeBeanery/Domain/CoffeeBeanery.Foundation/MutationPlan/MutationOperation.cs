using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

namespace CoffeeBeanery.GraphQL.Core.Foundation.MutationPlan;

/// <summary>
/// A mutation isn't a query: separate planner, optimizer, and executor
/// from QueryPlan/ProviderPlan.
/// </summary>
public abstract record MutationOperation;

public sealed record EntityMutation(
    EntityMetadata Entity,
    MutationKind Kind,
    IReadOnlyList<MutationColumn> Columns
) : MutationOperation;

public sealed record GraphMutation(
    GraphMetadata Graph,
    EntityMutation From,
    EntityMutation To
) : MutationOperation;

public sealed record RelationshipMutation(
    EntityMetadata Parent,
    EntityMetadata Child,
    JoinCondition Condition
) : MutationOperation;
