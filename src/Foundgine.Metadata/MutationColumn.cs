namespace Foundgine.Metadata;

public sealed record MutationColumn(
    ColumnReference Column,
    ushort SourceFieldId,
    MutationValueKind ValueKind,
    bool IsPrimaryKey = false
);