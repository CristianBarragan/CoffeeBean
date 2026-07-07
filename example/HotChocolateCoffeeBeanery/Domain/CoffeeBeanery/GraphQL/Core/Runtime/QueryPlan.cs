using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public enum JoinKind : byte { Left, Inner }

/// <summary>
/// One JOIN in the query plan.
/// FromEntityId/ToEntityId are model IDs (EntityId.*) — used for dispatch.
/// FromStorageEntityId/ToStorageEntityId are storage entity IDs (StorageEntityId.*) —
/// used by PostgresSqlWriter to look up schema, table name, and column names.
/// </summary>
public readonly struct JoinSpec
{
    public readonly ushort FromEntityId;
    public readonly ushort FromStorageEntityId;
    public readonly ushort ToEntityId;
    public readonly ushort ToStorageEntityId;
    public readonly ushort FromColumnId;
    public readonly ushort ToColumnId;
    public readonly JoinKind Kind;
    public readonly string ToOutputAlias;

    public JoinSpec(
        ushort fromEntityId,
        ushort fromStorageEntityId,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort fromColumnId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        FromEntityId        = fromEntityId;
        FromStorageEntityId = fromStorageEntityId;
        ToEntityId          = toEntityId;
        ToStorageEntityId   = toStorageEntityId;
        FromColumnId        = fromColumnId;
        ToColumnId          = toColumnId;
        Kind                = kind;
        ToOutputAlias       = toOutputAlias;
    }
}

/// <summary>
/// One column in the SELECT list.
/// EntityId is the model ID — used for FieldId dispatch.
/// StorageEntityId is the storage entity ID — used by PostgresSqlWriter
/// to look up EntityMeta.EntityColumnName[StorageEntityId][ColumnId].
/// ColumnId is an index into the storage entity's alphabetically-sorted
/// scalar properties (ColumnId.{EntityName}.*).
/// </summary>
public readonly struct ColumnSpec
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly ushort ColumnId;
    public readonly string EntityOutputAlias;
    public readonly string ColumnOutputAlias;

    public ColumnSpec(
        ushort entityId,
        ushort storageEntityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        EntityId          = entityId;
        StorageEntityId   = storageEntityId;
        ColumnId          = columnId;
        EntityOutputAlias = entityOutputAlias;
        ColumnOutputAlias = columnOutputAlias;
    }
}

public readonly struct QueryPlan
{
    public readonly ushort RootEntityId;
    public readonly ushort RootStorageEntityId;
    public readonly string RootOutputAlias;
    public readonly ImmutableArray<ColumnSpec> Columns;
    public readonly ImmutableArray<JoinSpec> Joins;

    public QueryPlan(
        ushort rootEntityId,
        ushort rootStorageEntityId,
        string rootOutputAlias,
        ImmutableArray<ColumnSpec> columns,
        ImmutableArray<JoinSpec> joins)
    {
        RootEntityId        = rootEntityId;
        RootStorageEntityId = rootStorageEntityId;
        RootOutputAlias     = rootOutputAlias;
        Columns             = columns;
        Joins               = joins;
    }
}

public ref struct QueryPlanBuilder
{
    private ushort _rootEntityId;
    private ushort _rootStorageEntityId;
    private string? _rootOutputAlias;

    private InlineArray64<ColumnSpec> _columns;
    private InlineArray32<JoinSpec>   _joins;
    private int _columnCount;
    private int _joinCount;

    public void SetRoot(ushort entityId, ushort storageEntityId, string outputAlias)
    {
        _rootEntityId        = entityId;
        _rootStorageEntityId = storageEntityId;
        _rootOutputAlias     = outputAlias;
    }

    /// <summary>
    /// entityId        — model ID (EntityId.*)         — used for FieldId dispatch
    /// storageEntityId — storage entity ID (StorageEntityId.*) — used for SQL column lookup
    /// columnId        — ColumnId.{EntityName}.* — index into EntityColumnName[storageEntityId]
    /// </summary>
    public void AddColumn(
        ushort entityId,
        ushort storageEntityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        _columns[_columnCount++] = new ColumnSpec(
            entityId, storageEntityId, columnId, entityOutputAlias, columnOutputAlias);
    }

    public void AddJoin(
        ushort fromEntityId,
        ushort fromStorageEntityId,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort fromColumnId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        _joins[_joinCount++] = new JoinSpec(
            fromEntityId, fromStorageEntityId,
            toEntityId,   toStorageEntityId,
            fromColumnId, toColumnId,
            kind, toOutputAlias);
    }

    public QueryPlan Build()
    {
        var cols = ImmutableArray.CreateBuilder<ColumnSpec>(_columnCount);
        for (var i = 0; i < _columnCount; i++) cols.Add(_columns[i]);

        var joins = ImmutableArray.CreateBuilder<JoinSpec>(_joinCount);
        for (var i = 0; i < _joinCount; i++) joins.Add(_joins[i]);

        return new QueryPlan(
            _rootEntityId,
            _rootStorageEntityId,
            _rootOutputAlias ?? string.Empty,
            cols.MoveToImmutable(),
            joins.MoveToImmutable());
    }
}

[System.Runtime.CompilerServices.InlineArray(64)]
internal struct InlineArray64<T> { private T _e0; }

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct InlineArray32<T> { private T _e0; }