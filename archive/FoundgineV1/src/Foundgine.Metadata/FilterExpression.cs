namespace Foundgine.Metadata;

/// <summary>
/// Comparison operators supported by <see cref="ComparisonFilter"/>.
/// Deliberately just the six from Milestone 6 — no LIKE, IN, IS NULL, etc.
/// yet. Adding one is a matter of extending this enum and
/// <c>Foundgine.Providers.SqlTextTranslator</c>'s operator-to-SQL mapping;
/// nothing upstream (QueryIntent, QueryPlanner, QueryNode) needs to change.
/// </summary>
public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

/// <summary>How <see cref="CompositeFilter.Operands"/> combine.</summary>
public enum FilterCombinator
{
    And,
    Or,
}

/// <summary>
/// A provider-agnostic boolean condition, expressed purely in terms of
/// <see cref="Foundgine.Metadata"/> identities — the filter counterpart of
/// <see cref="Foundgine.Builders.QueryNode"/>: describes WHAT must be true,
/// never HOW a specific backend expresses it (SQL WHERE, a graph
/// traversal predicate, an in-memory cache filter, ...).
/// </summary>
public abstract record FilterExpression;

/// <summary>
/// One column compared against a literal value, e.g.
/// <c>Customer.Name = "Bob"</c> or <c>Account.Balance &gt; 100</c>.
/// </summary>
public sealed record ComparisonFilter(
    ColumnReference Column,
    ComparisonOperator Operator,
    object? Value
) : FilterExpression;

/// <summary>
/// Two or more <see cref="FilterExpression"/>s combined with AND/OR, e.g.
/// <c>Customer.Name = "Bob" AND Account.Balance &gt; 100</c>. Nest
/// <see cref="CompositeFilter"/>s to express arbitrary boolean structure.
/// </summary>
public sealed record CompositeFilter(
    FilterCombinator Combinator,
    IReadOnlyList<FilterExpression> Operands
) : FilterExpression;
