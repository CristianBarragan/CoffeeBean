namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

public sealed record VertexMetadata(
    EntityMetadata Entity,
    ColumnReference KeyColumn,
    string Label,
    string GraphProperty,
    string Alias,
    string JoinColumn
);
