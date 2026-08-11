namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

/// <summary>
/// A single universal way to point at "a column on an entity". Used by
/// joins, field bindings, and vertex keys instead of each having its own
/// entity-id/column-id pair.
/// </summary>
public sealed record ColumnReference(
    EntityMetadata Entity,
    ushort ColumnId
);
