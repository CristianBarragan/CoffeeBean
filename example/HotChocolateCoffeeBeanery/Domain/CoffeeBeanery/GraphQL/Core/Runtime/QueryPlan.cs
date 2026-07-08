using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public enum JoinKind : byte { Left, Inner }

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
        ushort fromEntityId, ushort fromStorageEntityId,
        ushort toEntityId,   ushort toStorageEntityId,
        ushort fromColumnId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
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

public readonly struct ColumnSpec
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly ushort ColumnId;
    public readonly string EntityOutputAlias;
    public readonly string ColumnOutputAlias;

    public ColumnSpec(
        ushort entityId, ushort storageEntityId, ushort columnId,
        string entityOutputAlias, string columnOutputAlias)
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
        ushort rootEntityId, ushort rootStorageEntityId, string rootOutputAlias,
        ImmutableArray<ColumnSpec> columns, ImmutableArray<JoinSpec> joins)
    {
        RootEntityId        = rootEntityId;
        RootStorageEntityId = rootStorageEntityId;
        RootOutputAlias     = rootOutputAlias;
        Columns             = columns;
        Joins               = joins;
    }

    /// <summary>
    /// Builds a column map for one segment (one storage entity under one alias).
    /// Filters by both storageEntityId AND entityOutputAlias so InnerCustomer
    /// and OuterCustomer get separate maps even though both are StorageEntityId.Customer.
    /// </summary>
    public ushort[] BuildColumnMap(
        ushort storageEntityId,
        string entityOutputAlias,
        ushort columnCount)
    {
        var map = new ushort[columnCount];
        for (int i = 0; i < columnCount; i++)
            map[i] = ushort.MaxValue;

        ushort ordinal = 0;
        foreach (var col in Columns)
        {
            if (col.StorageEntityId == storageEntityId &&
                string.Equals(col.EntityOutputAlias, entityOutputAlias,
                    StringComparison.OrdinalIgnoreCase))
                map[col.ColumnId] = ordinal;
            ordinal++;
        }

        return map;
    }
}

public ref struct QueryPlanBuilder
{
    private ushort  _rootEntityId;
    private ushort  _rootStorageEntityId;
    private string? _rootOutputAlias;

    private InlineArray64<ColumnSpec> _columns;
    private InlineArray32<JoinSpec>   _joins;

    // Tracks aliases for root-segment columns only.
    // Join-segment columns always use a deterministic entity-alias prefix.
    private HashSet<string>? _rootAliases;

    private int _columnCount;
    private int _joinCount;

    public void SetRoot(ushort entityId, ushort storageEntityId, string outputAlias)
    {
        _rootEntityId        = entityId;
        _rootStorageEntityId = storageEntityId;
        _rootOutputAlias     = outputAlias;
    }

    /// <summary>
    /// Adds a column that belongs to the root segment.
    /// Deduplicates aliases within the root using fallback prefixing.
    /// Use for: composite primary-entity columns, simple-model columns.
    /// </summary>
    public void AddRootColumn(
        ushort entityId, ushort storageEntityId, ushort columnId,
        string entityOutputAlias, string columnOutputAlias)
    {
        _rootAliases ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var finalAlias = columnOutputAlias;

        if (!_rootAliases.Add(finalAlias))
        {
            finalAlias = entityOutputAlias +
                         char.ToUpperInvariant(columnOutputAlias[0]) +
                         columnOutputAlias[1..];

            if (!_rootAliases.Add(finalAlias))
            {
                finalAlias = $"{entityOutputAlias}_{columnOutputAlias}";
                _rootAliases.Add(finalAlias);
            }
        }

        _columns[_columnCount++] = new ColumnSpec(
            entityId, storageEntityId, columnId, entityOutputAlias, finalAlias);
    }

    /// <summary>
    // In QueryPlanBuilder.AddColumn (join segment path)
    public void AddColumn(
        ushort entityId, ushort storageEntityId, ushort columnId,
        string entityOutputAlias, string columnOutputAlias)
    {
        // Suffix "_pk" on "Id" to avoid colliding with FK columns in the root segment
        // e.g. root has "InnerCustomerId" (FK), join has "InnerCustomer_Id" (surrogate PK)
        var baseName = string.Equals(columnOutputAlias, "id",
            StringComparison.OrdinalIgnoreCase)
            ? entityOutputAlias + "_pk"
            : entityOutputAlias + char.ToUpperInvariant(columnOutputAlias[0]) + columnOutputAlias[1..];

        _columns[_columnCount++] = new ColumnSpec(
            entityId, storageEntityId, columnId, entityOutputAlias, baseName);
    }

    public void AddJoin(
        ushort fromEntityId, ushort fromStorageEntityId,
        ushort toEntityId,   ushort toStorageEntityId,
        ushort fromColumnId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
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
            _rootEntityId, _rootStorageEntityId,
            _rootOutputAlias ?? string.Empty,
            cols.MoveToImmutable(),
            joins.MoveToImmutable());
    }
}

[System.Runtime.CompilerServices.InlineArray(64)]
internal struct InlineArray64<T> { private T _e0; }

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct InlineArray32<T> { private T _e0; }