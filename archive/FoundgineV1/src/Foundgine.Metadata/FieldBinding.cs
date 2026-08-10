namespace Foundgine.Metadata;

/// <summary>Binds a source column to a target model field.</summary>
public sealed record FieldBinding(
    ColumnReference Source,
    ushort TargetFieldId
);
