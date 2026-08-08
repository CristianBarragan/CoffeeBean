namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record JoinCondition(
    ColumnReference Left,
    ColumnReference Right
);
