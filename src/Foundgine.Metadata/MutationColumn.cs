namespace Foundgine.Metadata;

/// <summary>
/// One column being written by a mutation: which column, where its value
/// came from (<see cref="ValueKind"/>), and — for
/// <see cref="MutationValueKind.Input"/>/<see cref="MutationValueKind.Constant"/>
/// — the literal <see cref="Value"/> to write. This is the mutation
/// counterpart of <see cref="ComparisonFilter.Value"/> on the read side:
/// the literal travels with the column reference instead of living in a
/// side table keyed by <see cref="SourceFieldId"/>, so a compiler never has
/// to reunite the two.
///
/// <see cref="Value"/> is meaningless for <see cref="MutationValueKind.Generated"/>
/// (e.g. an AUTOINCREMENT primary key) and <see cref="MutationValueKind.Expression"/>
/// (a computed SQL expression) — those kinds don't have a literal to carry
/// and are not yet compiled by <see cref="Foundgine.Providers.SqlTextTranslator"/>.
/// </summary>
public sealed record MutationColumn(
    ColumnReference Column,
    ushort SourceFieldId,
    MutationValueKind ValueKind,
    bool IsPrimaryKey = false,
    object? Value = null
);