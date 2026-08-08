namespace Foundgine.Metadata;

public sealed record ModelMetadata(
    ModelId Id,
    string Name,
    Type ClrType,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<ModelEntityBinding> Entities,
    ColumnReference? PrimaryKey = null
);

public sealed record ModelEntityBinding(
    EntityMetadata Entity,
    JoinCondition? JoinToParent
);