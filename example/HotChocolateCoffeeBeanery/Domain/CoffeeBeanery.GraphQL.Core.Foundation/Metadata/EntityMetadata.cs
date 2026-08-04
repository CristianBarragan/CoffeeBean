namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record EntityMetadata(
    EntityId EntityId,
    string Name,
    IReadOnlyList<ColumnMetadata> Columns
);
