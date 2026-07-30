

using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public ref struct MutationPlanBuilder
{
    private InlineArray32<UpsertRow> _rows;
    private int _rowCount;

    private InlineArray32<MutationCteNode> _cteRoots;
    private int _cteRootCount;

    private InlineArray32<GraphMergeSpec> _graphMerges;
    private int _graphMergeCount;

    private readonly HashSet<GraphMergeKey> _graphMergeKeys;


    public MutationPlanBuilder()
    {
        _rows = default;
        _rowCount = 0;

        _cteRoots = default;
        _cteRootCount = 0;

        _graphMerges = default;
        _graphMergeCount = 0;

        _graphMergeKeys = new HashSet<GraphMergeKey>();
    }

    public void AddRow(
        ushort entityId,
        ushort storageEntityId,
        string outputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null)
    {
        if (values.IsDefault)
        {
            values = ImmutableArray<FieldValue>.Empty;
        }

        _rows[_rowCount++] =
            new UpsertRow(
                entityId,
                storageEntityId,
                outputAlias,
                values,
                schemaOverride,
                tableOverride);
    }


    public void AddCteRoot(
        MutationCteNode node)
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
        ImmutableDictionary<string,string> edgeProperties)
    {
        var key =
            new GraphMergeKey(
                graphName,
                edgeLabel,
                fromLabel,
                fromKeyColumn,
                fromKeyValue,
                toLabel,
                toKeyColumn,
                toKeyValue);


        if (!_graphMergeKeys.Add(key))
            return;


        _graphMerges[_graphMergeCount++] =
            new GraphMergeSpec(
                graphName,
                edgeLabel,
                fromLabel,
                fromKeyColumn,
                fromKeyValue,
                toLabel,
                toKeyColumn,
                toKeyValue,
                edgeKeyColumn,
                edgeKeyValue,
                edgeProperties);
    }


    public MutationPlan Build()
    {
        var rows =
            ImmutableArray.CreateBuilder<UpsertRow>(_rowCount);

        for (var i = 0; i < _rowCount; i++)
            rows.Add(_rows[i]);


        var ctes =
            ImmutableArray.CreateBuilder<MutationCteNode>(_cteRootCount);

        for (var i = 0; i < _cteRootCount; i++)
            ctes.Add(_cteRoots[i]);


        var merges =
            ImmutableArray.CreateBuilder<GraphMergeSpec>(_graphMergeCount);

        for (var i = 0; i < _graphMergeCount; i++)
            merges.Add(_graphMerges[i]);


        return new MutationPlan(
            rows.ToImmutable(),
            ctes.ToImmutable(),
            merges.ToImmutable());
    }
}