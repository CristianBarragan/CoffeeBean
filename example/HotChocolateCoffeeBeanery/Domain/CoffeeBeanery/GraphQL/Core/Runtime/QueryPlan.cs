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

public readonly struct GraphJoinSpec
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly string GraphName;
    public readonly string EdgeLabel;
    public readonly string EdgeKeyColumn;
    public readonly string FromLabel;
    public readonly string FromKeyColumn;
    public readonly string FromAlias;
    public readonly string FromJoinColumn;
    public readonly string ToLabel;
    public readonly string ToKeyColumn;
    public readonly string ToAlias;
    public readonly string ToJoinColumn;
    public readonly string JoinAlias;

    public GraphJoinSpec(
        ushort entityId, ushort storageEntityId,
        string graphName, string edgeLabel, string edgeKeyColumn,
        string fromLabel, string fromKeyColumn, string fromAlias, string fromJoinColumn,
        string toLabel, string toKeyColumn, string toAlias, string toJoinColumn,
        string joinAlias)
    {
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        GraphName = graphName;
        EdgeLabel = edgeLabel;
        EdgeKeyColumn = edgeKeyColumn;
        FromLabel = fromLabel;
        FromKeyColumn = fromKeyColumn;
        FromAlias = fromAlias;
        FromJoinColumn = fromJoinColumn;
        ToLabel = toLabel;
        ToKeyColumn = toKeyColumn;
        ToAlias = toAlias;
        ToJoinColumn = toJoinColumn;
        JoinAlias = joinAlias;
    }
}

public readonly struct GraphMergeSpec
{
    public readonly string GraphName;
    public readonly string EdgeLabel;
    public readonly string FromLabel;
    public readonly string FromKeyColumn;
    public readonly string FromKeyValue;
    public readonly string ToLabel;
    public readonly string ToKeyColumn;
    public readonly string ToKeyValue;
    public readonly string EdgeKeyColumn;
    public readonly string? EdgeKeyValue;
    public readonly ImmutableDictionary<string, string> EdgeProperties;

    public GraphMergeSpec(
        string graphName, string edgeLabel,
        string fromLabel, string fromKeyColumn, string fromKeyValue,
        string toLabel, string toKeyColumn, string toKeyValue,
        string edgeKeyColumn, string? edgeKeyValue,
        ImmutableDictionary<string, string> edgeProperties)
    {
        GraphName = graphName;
        EdgeLabel = edgeLabel;
        FromLabel = fromLabel;
        FromKeyColumn = fromKeyColumn;
        FromKeyValue = fromKeyValue;
        ToLabel = toLabel;
        ToKeyColumn = toKeyColumn;
        ToKeyValue = toKeyValue;
        EdgeKeyColumn = edgeKeyColumn;
        EdgeKeyValue = edgeKeyValue;
        EdgeProperties = edgeProperties ?? ImmutableDictionary<string, string>.Empty;
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
    public readonly ImmutableArray<GraphJoinSpec> GraphJoins;

    public QueryPlan(
        ushort rootEntityId, ushort rootStorageEntityId, string rootOutputAlias,
        ImmutableArray<ColumnSpec> columns, ImmutableArray<JoinSpec> joins,
        ImmutableArray<GraphJoinSpec> graphJoins)
    {
        RootEntityId        = rootEntityId;
        RootStorageEntityId = rootStorageEntityId;
        RootOutputAlias     = rootOutputAlias;
        Columns             = columns;
        Joins               = joins;
        GraphJoins          = graphJoins;
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
    private InlineArray32<GraphJoinSpec> _graphJoins;
    private int _graphJoinCount;

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
    
    public void AddGraphJoin(
        ushort entityId,
        ushort storageEntityId,
        string graphName,
        string edgeLabel,
        string edgeKeyColumn,
        string fromLabel,
        string fromKeyColumn,
        string fromAlias,
        string fromJoinColumn,
        string toLabel,
        string toKeyColumn,
        string toAlias,
        string toJoinColumn,
        string joinAlias)
    {
        _graphJoins[_graphJoinCount++] = new GraphJoinSpec(
            entityId, storageEntityId,
            graphName, edgeLabel, edgeKeyColumn,
            fromLabel, fromKeyColumn, fromAlias, fromJoinColumn,
            toLabel, toKeyColumn, toAlias, toJoinColumn,
            joinAlias);
    }

    public QueryPlan Build()
    {
        var cols = ImmutableArray.CreateBuilder<ColumnSpec>(_columnCount);
        for (var i = 0; i < _columnCount; i++) cols.Add(_columns[i]);

        var joins = ImmutableArray.CreateBuilder<JoinSpec>(_joinCount);
        for (var i = 0; i < _joinCount; i++) joins.Add(_joins[i]);
        
        var graphJoins = ImmutableArray.CreateBuilder<GraphJoinSpec>(_graphJoinCount);
        for (var i = 0; i < _graphJoinCount; i++) graphJoins.Add(_graphJoins[i]);

        return new QueryPlan(
            _rootEntityId, _rootStorageEntityId,
            _rootOutputAlias ?? string.Empty,
            cols.MoveToImmutable(),
            joins.MoveToImmutable(),
            graphJoins.MoveToImmutable());
    }
}

[System.Runtime.CompilerServices.InlineArray(64)]
internal struct InlineArray64<T> { private T _e0; }

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct InlineArray32<T> { private T _e0; }