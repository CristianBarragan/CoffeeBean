namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record JoinMetadata(
    JoinCondition Condition,
    JoinKind Kind
);
