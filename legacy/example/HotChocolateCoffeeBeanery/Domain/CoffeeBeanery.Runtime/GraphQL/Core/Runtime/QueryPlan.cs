using System.Collections.Immutable;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public enum JoinKind : byte
{
    Left,
    Inner
}

public enum JoinSourceKind : byte
{
    Table,
    GraphVertex
}

public readonly struct JoinSpec
{
    // SQL aliases
    public readonly string ParentAlias;
    public readonly string ChildAlias;

    // Parent table
    public readonly ushort ParentEntityId;
    public readonly ushort ParentStorageEntityId;
    public readonly ushort ParentColumnId;

    // Child table
    public readonly ushort ChildEntityId;
    public readonly ushort ChildStorageEntityId;
    public readonly ushort ChildColumnId;

    public readonly JoinKind Kind;

    public JoinSpec(
        string parentAlias,
        string childAlias,
        ushort parentEntityId,
        ushort parentStorageEntityId,
        ushort parentColumnId,
        ushort childEntityId,
        ushort childStorageEntityId,
        ushort childColumnId,
        JoinKind kind)
    {
        ParentAlias = parentAlias;
        ChildAlias = childAlias;

        ParentEntityId = parentEntityId;
        ParentStorageEntityId = parentStorageEntityId;
        ParentColumnId = parentColumnId;

        ChildEntityId = childEntityId;
        ChildStorageEntityId = childStorageEntityId;
        ChildColumnId = childColumnId;

        Kind = kind;
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
    public readonly string FromGraphProperty;
    public readonly string FromAlias;
    public readonly string FromJoinColumn;

    public readonly string ToLabel;
    public readonly string ToGraphProperty;
    public readonly string ToAlias;
    public readonly string ToJoinColumn;

    public readonly string JoinAlias;


    public GraphJoinSpec(
        ushort entityId,
        ushort storageEntityId,
        string graphName,
        string edgeLabel,
        string edgeKeyColumn,
        string fromLabel,
        string fromGraphProperty,
        string fromAlias,
        string fromJoinColumn,
        string toLabel,
        string toGraphProperty,
        string toAlias,
        string toJoinColumn,
        string joinAlias)
    {
        EntityId = entityId;
        StorageEntityId = storageEntityId;

        GraphName = graphName;
        EdgeLabel = edgeLabel;
        EdgeKeyColumn = edgeKeyColumn;

        FromLabel = fromLabel;
        FromGraphProperty = fromGraphProperty;
        FromAlias = fromAlias;
        FromJoinColumn = fromJoinColumn;

        ToLabel = toLabel;
        ToGraphProperty = toGraphProperty;
        ToAlias = toAlias;
        ToJoinColumn = toJoinColumn;

        JoinAlias = joinAlias;
    }
}


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
        string fromAlias,
        string fromColumnName,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
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

    public readonly ImmutableDictionary<string,string> EdgeProperties;

    public readonly string EdgePropertiesHash;


    public GraphMergeSpec(
        string graphName,
        string edgeLabel,
        string fromLabel,
        string fromKeyColumn,
        string fromKeyValue,
        string toLabel,
        string toKeyColumn,
        string toKeyValue,
        string edgeKeyColumn,
        string? edgeKeyValue,
        ImmutableDictionary<string,string> edgeProperties)
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

        EdgeProperties =
            edgeProperties ??
            ImmutableDictionary<string,string>.Empty;

        EdgePropertiesHash =
            GraphMergeKey.NormalizeProperties(EdgeProperties);
    }
}


public enum ColumnKind : byte
{
    Table,
    GraphSynthetic
}


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
        ushort entityId,
        ushort storageEntityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        Kind = ColumnKind.Table;
        EntityId = entityId;
        StorageEntityId = storageEntityId;
        ColumnId = columnId;
        RawColumnName = null;

        EntityOutputAlias = entityOutputAlias;
        ColumnOutputAlias = columnOutputAlias;
    }


    public ColumnSpec(
        ushort entityId,
        string rawColumnName,
        string entityOutputAlias,
        string columnOutputAlias)
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

    public readonly EntityFilterMetadata? EntityFilterMetadata;
    public string RootAlias { get; }

    public QueryPlan(
        ushort rootEntityId,
        ushort rootStorageEntityId,
        string rootAlias,
        ImmutableArray<ColumnSpec> columns,
        ImmutableArray<JoinSpec> joins,
        ImmutableArray<GraphJoinSpec> graphJoins,
        ImmutableArray<GraphResultJoinSpec> graphResultJoins)
    {
        RootEntityId = rootEntityId;
        RootStorageEntityId = rootStorageEntityId;
        RootAlias = rootAlias;
        RootOutputAlias = rootAlias;
        Columns = columns;
        Joins = joins;
        GraphJoins = graphJoins;
        GraphResultJoins = graphResultJoins;
    }


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
                string.Equals(
                    col.EntityOutputAlias,
                    entityOutputAlias,
                    StringComparison.OrdinalIgnoreCase))
            {
                map[col.ColumnId] = ordinal;
            }

            ordinal++;
        }

        return map;
    }
}

public readonly struct JoinKey : IEquatable<JoinKey>
{
    public readonly ushort FromEntityId;
    public readonly ushort FromStorageEntityId;
    public readonly ushort FromColumnId;

    public readonly ushort ToEntityId;
    public readonly ushort ToStorageEntityId;
    public readonly ushort ToColumnId;

    public readonly string ToOutputAlias;


    public JoinKey(
        ushort fromEntityId,
        ushort fromStorageEntityId,
        ushort fromColumnId,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort toColumnId,
        string toOutputAlias)
    {
        FromEntityId = fromEntityId;
        FromStorageEntityId = fromStorageEntityId;
        FromColumnId = fromColumnId;

        ToEntityId = toEntityId;
        ToStorageEntityId = toStorageEntityId;
        ToColumnId = toColumnId;

        ToOutputAlias = toOutputAlias;
    }


    public bool Equals(JoinKey other)
    {
        return
            FromEntityId == other.FromEntityId &&
            FromStorageEntityId == other.FromStorageEntityId &&
            FromColumnId == other.FromColumnId &&

            ToEntityId == other.ToEntityId &&
            ToStorageEntityId == other.ToStorageEntityId &&
            ToColumnId == other.ToColumnId &&

            string.Equals(
                ToOutputAlias,
                other.ToOutputAlias,
                StringComparison.OrdinalIgnoreCase);
    }


    public override bool Equals(object? obj)
        => obj is JoinKey other && Equals(other);


    public override int GetHashCode()
    {
        return HashCode.Combine(
            FromEntityId,
            FromStorageEntityId,
            FromColumnId,
            ToEntityId,
            ToStorageEntityId,
            ToColumnId,
            StringComparer.OrdinalIgnoreCase.GetHashCode(ToOutputAlias));
    }
}


internal readonly record struct GraphJoinKey(
    ushort FromEntityId,
    string FromAlias,
    string FromColumn,
    ushort ToEntityId,
    ushort ToStorageEntityId,
    ushort ToColumnId);


public readonly record struct GraphMergeKey(
    string GraphName,
    string EdgeLabel,

    string FromLabel,
    string FromKeyColumn,
    string FromKeyValue,

    string ToLabel,
    string ToKeyColumn,
    string ToKeyValue,

    string EdgeKeyColumn,
    string? EdgeKeyValue,

    string EdgePropertiesHash)
{
    public static string NormalizeProperties(
        ImmutableDictionary<string,string> properties)
    {
        return string.Join(
            "|",
            properties
                .OrderBy(
                    x => x.Key,
                    StringComparer.Ordinal)
                .Select(
                    x => $"{x.Key}={x.Value}"));
    }
}