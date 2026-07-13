using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public readonly struct UpsertRow
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly string EntityOutputAlias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly string? SchemaOverride;
    public readonly string? TableOverride;

    public UpsertRow(
        ushort entityId,
        ushort storageEntityId,
        string entityOutputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null)
    {
        EntityId        = entityId;
        StorageEntityId = storageEntityId;
        EntityOutputAlias = entityOutputAlias;
        Values          = values;
        SchemaOverride  = schemaOverride;
        TableOverride   = tableOverride;
    }
}

public readonly struct CteResolutionSpec
{
    public readonly string NavigationAlias;
    public readonly string ForeignKeyColumn;
    public readonly string OwningPkColumn;
    public readonly ushort OwningPkFieldId;
    public readonly string RelatedTableAlias;
    public readonly string RelatedSurrogateIdColumn;
    public readonly string RelatedNaturalKeyColumn;

    public CteResolutionSpec(
        string navigationAlias,
        string foreignKeyColumn,
        string owningPkColumn,
        ushort owningPkFieldId,
        string relatedTableAlias,
        string relatedSurrogateIdColumn,
        string relatedNaturalKeyColumn)
    {
        NavigationAlias = navigationAlias;
        ForeignKeyColumn = foreignKeyColumn;
        OwningPkColumn = owningPkColumn;
        OwningPkFieldId = owningPkFieldId;
        RelatedTableAlias = relatedTableAlias;
        RelatedSurrogateIdColumn = relatedSurrogateIdColumn;
        RelatedNaturalKeyColumn = relatedNaturalKeyColumn;
    }
}

public readonly struct MutationCteNode
{
    public readonly ushort EntityId;
    public readonly ushort StorageEntityId;
    public readonly string Alias;
    public readonly ImmutableArray<FieldValue> Values;
    public readonly ImmutableArray<MutationCteNode> Children;
    public readonly string? SchemaOverride;
    public readonly string? TableOverride;
    public readonly ImmutableArray<string> ConflictColumns;

    public MutationCteNode(
        ushort entityId,
        ushort storageEntityId,
        string alias,
        ImmutableArray<FieldValue> values,
        ImmutableArray<MutationCteNode> children,
        string? schemaOverride = null,
        string? tableOverride = null,
        ImmutableArray<string> conflictColumns = default)
    {
        EntityId        = entityId;
        StorageEntityId = storageEntityId;
        Alias           = alias;
        Values          = values;
        Children        = children;
        SchemaOverride  = schemaOverride;
        TableOverride   = tableOverride;
        ConflictColumns = conflictColumns.IsDefault
            ? ImmutableArray<string>.Empty
            : conflictColumns;
    }
}

public readonly struct FieldMapSpec
{
    public readonly string SourceName;
    public readonly ushort DestinationEntity;
    public readonly string DestinationName;
    public readonly string SourceAlias;
    public readonly string DestinationAlias;

    public FieldMapSpec(
        string sourceName,
        ushort destinationEntity,
        string destinationName,
        string sourceAlias,
        string destinationAlias)
    {
        SourceName = sourceName;
        DestinationEntity = destinationEntity;
        DestinationName = destinationName;
        SourceAlias = sourceAlias;
        DestinationAlias = destinationAlias;
    }
}

public readonly struct MutationPlan
{
    public readonly ImmutableArray<UpsertRow> Rows;
    public readonly ImmutableArray<MutationCteNode> CteRoots;
    public readonly ImmutableArray<GraphMergeSpec> GraphMerges;

    public MutationPlan(ImmutableArray<UpsertRow> rows)
    {
        Rows = rows;
        CteRoots = ImmutableArray<MutationCteNode>.Empty;
        GraphMerges = ImmutableArray<GraphMergeSpec>.Empty;
    }

    public MutationPlan(
        ImmutableArray<UpsertRow> rows,
        ImmutableArray<MutationCteNode> cteRoots)
    {
        Rows = rows;
        CteRoots = cteRoots;
        GraphMerges = ImmutableArray<GraphMergeSpec>.Empty;
    }

    public MutationPlan(
        ImmutableArray<UpsertRow> rows,
        ImmutableArray<MutationCteNode> cteRoots,
        ImmutableArray<GraphMergeSpec> graphMerges)
    {
        Rows = rows;
        CteRoots = cteRoots;
        GraphMerges = graphMerges;
    }

    public bool HasCte => !CteRoots.IsEmpty;
    public bool HasGraphMerges => !GraphMerges.IsEmpty;
}

public ref struct MutationPlanBuilder
{
    private InlineArray32<UpsertRow> _rows;
    private int _rowCount;

    private InlineArray32<MutationCteNode> _cteRoots;
    private int _cteRootCount;

    private InlineArray32<GraphMergeSpec> _graphMerges;
    private int _graphMergeCount;

    public void AddRow(
        ushort entityId, ushort storageEntityId, string outputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null, string? tableOverride = null)
    {
        _rows[_rowCount++] = new UpsertRow(entityId, storageEntityId, outputAlias, values, schemaOverride, tableOverride);
    }

    public void AddCteRoot(MutationCteNode node)
    {
        _cteRoots[_cteRootCount++] = node;
    }

    public void AddGraphMerge(
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
        ImmutableDictionary<string, string> edgeProperties)
    {
        _graphMerges[_graphMergeCount++] = new GraphMergeSpec(
            graphName, edgeLabel,
            fromLabel, fromKeyColumn, fromKeyValue,
            toLabel, toKeyColumn, toKeyValue,
            edgeKeyColumn, edgeKeyValue,
            edgeProperties);
    }

    public MutationPlan Build()
    {
        var rows = ImmutableArray.CreateBuilder<UpsertRow>(_rowCount);
        for (var i = 0; i < _rowCount; i++)
            rows.Add(_rows[i]);

        var graphMerges = ImmutableArray.CreateBuilder<GraphMergeSpec>(_graphMergeCount);
        for (var i = 0; i < _graphMergeCount; i++)
            graphMerges.Add(_graphMerges[i]);

        if (_cteRootCount == 0)
            return new MutationPlan(rows.ToImmutable(), ImmutableArray<MutationCteNode>.Empty, graphMerges.ToImmutable());

        var roots = ImmutableArray.CreateBuilder<MutationCteNode>(_cteRootCount);
        for (var i = 0; i < _cteRootCount; i++)
            roots.Add(_cteRoots[i]);

        return new MutationPlan(rows.ToImmutable(), roots.ToImmutable(), graphMerges.ToImmutable());
    }
}