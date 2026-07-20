using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public enum JoinKind : byte { Left, Inner }

public enum JoinSourceKind : byte { Table, GraphVertex }

public readonly struct JoinSpec
{
    public readonly JoinSourceKind SourceKind;
    public readonly ushort FromEntityId;
    public readonly ushort FromStorageEntityId;
    public readonly ushort FromColumnId;
    public readonly string? FromGraphAlias;
    public readonly string? FromRawColumnName;
    public readonly ushort ToEntityId;
    public readonly ushort ToStorageEntityId;
    public readonly ushort ToColumnId;
    public readonly JoinKind Kind;
    public readonly string ToOutputAlias;
    
    public JoinSpec(
        ushort fromEntityId, ushort fromStorageEntityId,
        ushort toEntityId,   ushort toStorageEntityId,
        ushort fromColumnId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
    {
        SourceKind = JoinSourceKind.Table;
        FromEntityId = fromEntityId;
        FromStorageEntityId = fromStorageEntityId;
        FromColumnId = fromColumnId;
        FromGraphAlias = null;
        FromRawColumnName = null;
        ToEntityId = toEntityId;
        ToStorageEntityId = toStorageEntityId;
        ToColumnId = toColumnId;
        Kind = kind;
        ToOutputAlias = toOutputAlias;
    }

    public JoinSpec(
        ushort fromEntityId, string fromGraphAlias, string fromRawColumnName,
        ushort toEntityId, ushort toStorageEntityId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
    {
        SourceKind = JoinSourceKind.GraphVertex;
        FromEntityId = fromEntityId;
        FromStorageEntityId = 0;
        FromColumnId = 0;
        FromGraphAlias = fromGraphAlias;
        FromRawColumnName = fromRawColumnName;
        ToEntityId = toEntityId;
        ToStorageEntityId = toStorageEntityId;
        ToColumnId = toColumnId;
        Kind = kind;
        ToOutputAlias = toOutputAlias;
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

/// <summary>
/// Joins a real stored table against a column that lives on a graph subquery's
/// output (e.g. CustomerCustomerEdge_graph's InnerCustomerCustomerCustomerKey) —
/// which has no ColumnId since it isn't a stored column on any entity. Mirrors
/// JoinSpec, but the "from" side is addressed by a literal alias + raw column
/// name instead of a typed (StorageEntityId, ColumnId) pair.
/// </summary>
public readonly struct GraphResultJoinSpec
{
    public readonly string FromAlias;
    public readonly string FromColumnName;
    public readonly ushort ToEntityId;
    public readonly ushort ToStorageEntityId;
    public readonly ushort ToColumnId;
    public readonly JoinKind Kind;
    public readonly string ToOutputAlias;

    public GraphResultJoinSpec(
        string fromAlias, string fromColumnName,
        ushort toEntityId, ushort toStorageEntityId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
    {
        FromAlias = fromAlias;
        FromColumnName = fromColumnName;
        ToEntityId = toEntityId;
        ToStorageEntityId = toStorageEntityId;
        ToColumnId = toColumnId;
        Kind = kind;
        ToOutputAlias = toOutputAlias;
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

public enum ColumnKind : byte { Table, GraphSynthetic }

public readonly struct ColumnSpec
{
    public readonly ColumnKind Kind;
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly ushort ColumnId;
    public readonly string? RawColumnName;
    public readonly string EntityOutputAlias;
    public readonly string ColumnOutputAlias;
    
    public ColumnSpec(
        ushort entityId, ushort storageEntityId, ushort columnId,
        string entityOutputAlias, string columnOutputAlias)
    {
        Kind = ColumnKind.Table;
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        ColumnId = columnId;
        RawColumnName = null;
        EntityOutputAlias = entityOutputAlias;
        ColumnOutputAlias = columnOutputAlias;
    }

    // new constructor for graph-synthetic columns
    public ColumnSpec(
        ushort entityId, string rawColumnName,
        string entityOutputAlias, string columnOutputAlias)
    {
        Kind = ColumnKind.GraphSynthetic;
        EntityId = entityId;
        StorageEntityId = 0;
        ColumnId = 0;
        RawColumnName = rawColumnName;
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
    public readonly ImmutableArray<GraphResultJoinSpec> GraphResultJoins;

    public QueryPlan(
        ushort rootEntityId, ushort rootStorageEntityId, string rootOutputAlias,
        ImmutableArray<ColumnSpec> columns, ImmutableArray<JoinSpec> joins,
        ImmutableArray<GraphJoinSpec> graphJoins,
        ImmutableArray<GraphResultJoinSpec> graphResultJoins)
    {
        RootEntityId        = rootEntityId;
        RootStorageEntityId = rootStorageEntityId;
        RootOutputAlias     = rootOutputAlias;
        Columns             = columns;
        Joins               = joins;
        GraphJoins          = graphJoins;
        GraphResultJoins    = graphResultJoins;
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
    private InlineArray32<GraphResultJoinSpec> _graphResultJoins;
    private int _graphResultJoinCount;

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

        _columns[_columnCount++] = new ColumnSpec(entityId, storageEntityId, columnId, entityOutputAlias, finalAlias);
    }
    
    public void AddGraphVertexJoin(
        ushort fromEntityId, string fromGraphAlias, string fromRawColumnName,
        ushort toEntityId, ushort toStorageEntityId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
    {
        _joins[_joinCount++] = new JoinSpec(
            fromEntityId, fromGraphAlias, fromRawColumnName,
            toEntityId, toStorageEntityId, toColumnId,
            kind, toOutputAlias);
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
    
    public void AddGraphColumn(
        ushort entityId, string joinAlias, string rawColumnName, string columnOutputAlias)
    {
        _columns[_columnCount++] = new ColumnSpec(entityId, rawColumnName, joinAlias, columnOutputAlias);
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

    /// <summary>
    /// Joins a real stored table against a graph subquery's synthetic output
    /// column (e.g. CustomerCustomerEdge_graph.InnerCustomerCustomerCustomerKey
    /// -> Customer.CustomerKey), so scalar columns on that related entity
    /// (e.g. Customer.FullName under alias "InnerCustomer") can actually be
    /// selected rather than referencing a table alias that was never joined in.
    /// </summary>
    public void AddGraphResultJoin(
        string fromAlias, string fromColumnName,
        ushort toEntityId, ushort toStorageEntityId, ushort toColumnId,
        JoinKind kind, string toOutputAlias)
    {
        _graphResultJoins[_graphResultJoinCount++] = new GraphResultJoinSpec(
            fromAlias, fromColumnName,
            toEntityId, toStorageEntityId, toColumnId,
            kind, toOutputAlias);
    }

    public QueryPlan Build()
    {
        var cols = ImmutableArray.CreateBuilder<ColumnSpec>(_columnCount);
        for (var i = 0; i < _columnCount; i++) cols.Add(_columns[i]);

        var joins = ImmutableArray.CreateBuilder<JoinSpec>(_joinCount);
        for (var i = 0; i < _joinCount; i++) joins.Add(_joins[i]);
        
        var graphJoins = ImmutableArray.CreateBuilder<GraphJoinSpec>(_graphJoinCount);
        for (var i = 0; i < _graphJoinCount; i++) graphJoins.Add(_graphJoins[i]);

        var graphResultJoins = ImmutableArray.CreateBuilder<GraphResultJoinSpec>(_graphResultJoinCount);
        for (var i = 0; i < _graphResultJoinCount; i++) graphResultJoins.Add(_graphResultJoins[i]);

        return new QueryPlan(
            _rootEntityId, _rootStorageEntityId,
            _rootOutputAlias ?? string.Empty,
            cols.MoveToImmutable(),
            joins.MoveToImmutable(),
            graphJoins.MoveToImmutable(),
            graphResultJoins.MoveToImmutable());
    }
}

[System.Runtime.CompilerServices.InlineArray(64)]
internal struct InlineArray64<T> { private T _e0; }

[System.Runtime.CompilerServices.InlineArray(32)]
internal struct InlineArray32<T> { private T _e0; }