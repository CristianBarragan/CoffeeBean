using Foundgine.Execution;
using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Sql;

/// <summary>
/// Physical SQL representation of a provider-independent execution plan.
/// SQL exists only at this provider boundary.
/// </summary>
public sealed record SqlPlan(
    string CommandText,
    IReadOnlyList<SqlColumnBinding> Columns,
    IReadOnlyList<Foundgine.Sql.Query.SqlParameterBinding>? Parameters = null,
    SqlPaginationPlan? Pagination = null) : ProviderPlan("sql")
{
    public IReadOnlyList<Foundgine.Sql.Query.SqlParameterBinding> EffectiveParameters => Parameters ?? [];
}

public sealed record SqlColumnBinding(
    string ResultName,
    EntityId EntityId,
    FieldId FieldId,
    string ColumnName,
    int NodeId,
    bool IsCursor = false);

/// <summary>
/// Forward keyset pagination metadata. CursorValues describes the exact
/// semantic ordering tuple used by the SQL seek predicate.
/// </summary>
public sealed record SqlPaginationPlan(
    int First,
    IReadOnlyList<SqlCursorBinding> CursorValues,
    string? After);

public sealed record SqlCursorBinding(
    string ResultName,
    EntityId EntityId,
    FieldId FieldId,
    Type ClrType,
    SemanticSortDirection Direction);
