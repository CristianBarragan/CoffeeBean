using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public enum JoinKind : byte { Left, Inner }

/// <summary>
/// One JOIN in the query plan.
/// All IDs are generated ushort constants from IdEmitter.
/// ToOutputAlias is the role alias to use as the SQL table alias
/// (e.g. "InnerCustomerCustomer" rather than plain "Customer"
/// when the same table appears twice via different roles).
/// </summary>
public readonly struct JoinSpec
{
    public readonly ushort FromEntityId;
    public readonly ushort ToEntityId;
    public readonly ushort FromColumnId;
    public readonly ushort ToColumnId;
    public readonly JoinKind Kind;
    public readonly string ToOutputAlias;

    public JoinSpec(
        ushort fromEntityId,
        ushort toEntityId,
        ushort fromColumnId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        FromEntityId = fromEntityId;
        ToEntityId = toEntityId;
        FromColumnId = fromColumnId;
        ToColumnId = toColumnId;
        Kind = kind;
        ToOutputAlias = toOutputAlias;
    }
}

/// <summary>
/// One column in the SELECT list.
/// OutputAlias is the wire alias the client used (from SelectionIR).
/// </summary>
public readonly struct ColumnSpec
{
    public readonly ushort EntityId;
    public readonly ushort ColumnId;
    public readonly string EntityOutputAlias;
    public readonly string ColumnOutputAlias;

    public ColumnSpec(
        ushort entityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        EntityId = entityId;
        ColumnId = columnId;
        EntityOutputAlias = entityOutputAlias;
        ColumnOutputAlias = columnOutputAlias;
    }
}

/// <summary>
/// The complete, immutable plan for one query or mutation read-back.
/// Produced by a generated *Planner.BuildQuery method.
/// Consumed by PostgresSqlWriter.Write.
/// </summary>
public readonly struct QueryPlan
{
    public readonly ushort RootEntityId;
    public readonly string RootOutputAlias;
    public readonly ImmutableArray<ColumnSpec> Columns;
    public readonly ImmutableArray<JoinSpec> Joins;

    public QueryPlan(
        ushort rootEntityId,
        string rootOutputAlias,
        ImmutableArray<ColumnSpec> columns,
        ImmutableArray<JoinSpec> joins)
    {
        RootEntityId = rootEntityId;
        RootOutputAlias = rootOutputAlias;
        Columns = columns;
        Joins = joins;
    }
}

/// <summary>
/// Mutable builder passed by ref into generated planner methods.
/// Each generated planner calls AddColumn/AddJoin and passes
/// the builder ref down into child planners - no heap allocation
/// until Build() is called at the end.
/// </summary>
public ref struct QueryPlanBuilder
{
    private ushort _rootEntityId;
    private string? _rootOutputAlias;

    // Fixed-size stacks to avoid heap allocation during planning.
    // 64 columns and 32 joins covers every realistic schema depth.
    private InlineArray64<ColumnSpec> _columns;
    private InlineArray32<JoinSpec> _joins;
    private int _columnCount;
    private int _joinCount;

    public void SetRoot(ushort entityId, string outputAlias)
    {
        _rootEntityId = entityId;
        _rootOutputAlias = outputAlias;
    }

    public void AddColumn(
        ushort entityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        _columns[_columnCount++] = new ColumnSpec(
            entityId, columnId, entityOutputAlias, columnOutputAlias);
    }

    public void AddJoin(
        ushort fromEntityId,
        ushort toEntityId,
        ushort fromColumnId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        _joins[_joinCount++] = new JoinSpec(
            fromEntityId, toEntityId, fromColumnId, toColumnId, kind, toOutputAlias);
    }

    public QueryPlan Build()
    {
        var cols = ImmutableArray.CreateBuilder<ColumnSpec>(_columnCount);
        for (var i = 0; i < _columnCount; i++) cols.Add(_columns[i]);

        var joins = ImmutableArray.CreateBuilder<JoinSpec>(_joinCount);
        for (var i = 0; i < _joinCount; i++) joins.Add(_joins[i]);

        return new QueryPlan(
            _rootEntityId,
            _rootOutputAlias ?? string.Empty,
            cols.MoveToImmutable(),
            joins.MoveToImmutable());
    }
}

// Inline fixed-size arrays for stack allocation in QueryPlanBuilder.
// These avoid any heap allocation during the plan-build hot path.
[System.Runtime.CompilerServices.InlineArray(64)]
internal struct InlineArray64<T> { private T _e0; }

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct InlineArray32<T> { private T _e0; }