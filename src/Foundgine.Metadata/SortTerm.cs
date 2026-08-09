namespace Foundgine.Metadata;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// One ORDER BY term, provider-agnostic: which column, and which
/// direction. A <see cref="Foundgine.Planning.QueryIntent"/> carries an
/// ordered list of these; <see cref="Foundgine.Providers.SqlTextTranslator"/>
/// is what turns them into <c>ORDER BY ... ASC/DESC</c> — nothing upstream
/// of it knows SQL syntax exists.
/// </summary>
public sealed record SortTerm(
    ColumnReference Column,
    SortDirection Direction = SortDirection.Ascending
);
