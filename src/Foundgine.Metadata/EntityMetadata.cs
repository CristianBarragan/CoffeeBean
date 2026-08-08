namespace Foundgine.Metadata;

public sealed record EntityMetadata(
    EntityId EntityId,
    string Name,
    IReadOnlyList<ColumnMetadata> Columns
);
