namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

/// <summary>
/// One side (from/to) of a graph edge. ConnectedEntity is the storage
/// entity this vertex resolves to (matched by Label against the mapped
/// entity types, same convention the SQL-side planner already uses) --
/// carrying it here avoids a runtime string lookup to find it again.
/// </summary>
public sealed record VertexMetadata(
    string Label,
    string GraphProperty,
    string JoinColumn,
    string Alias,
    EntityMetadata ConnectedEntity
);