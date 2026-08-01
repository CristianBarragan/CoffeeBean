using System;
using System.Collections.Generic;
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

    private InlineArray32<MutationDependency> _dependencies;
    private int _dependencyCount;

    private readonly HashSet<GraphMergeKey> _graphMergeKeys;



    public MutationPlanBuilder()
    {
        _rows = default;
        _rowCount = 0;

        _cteRoots = default;
        _cteRootCount = 0;

        _graphMerges = default;
        _graphMergeCount = 0;

        _dependencies = default;
        _dependencyCount = 0;

        _graphMergeKeys =
            new HashSet<GraphMergeKey>();
    }



    public int AddRow(
        ushort entityId,
        ushort storageEntityId,
        string outputAlias,
        ImmutableArray<FieldValue> values,
        string? schemaOverride = null,
        string? tableOverride = null,
        ImmutableArray<LookupValue> lookups = default)
    {
        if (values.IsDefault)
            values = ImmutableArray<FieldValue>.Empty;

        if (lookups.IsDefault)
            lookups = ImmutableArray<LookupValue>.Empty;


        var index = _rowCount;


        _rows[_rowCount++] =
            new UpsertRow(
                entityId,
                storageEntityId,
                outputAlias,
                values,
                schemaOverride,
                tableOverride,
                lookups);


        return index;
    }



    public int FindRowIndex(string alias)
    {
        for (var i = 0; i < _rowCount; i++)
        {
            if (string.Equals(
                    _rows[i].EntityOutputAlias,
                    alias,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }



    public bool TryGetRow(
        string alias,
        out int rowIndex)
    {
        rowIndex = FindRowIndex(alias);

        return rowIndex >= 0;
    }



    public void AddDependency(
        int sourceRow,
        int targetRow,
        string sourceColumn,
        string targetColumn)
    {
        if (sourceRow < 0 ||
            targetRow < 0)
        {
            return;
        }


        _dependencies[_dependencyCount++] =
            new MutationDependency(
                sourceRow,
                targetRow,
                sourceColumn,
                targetColumn);
    }



    public void AddCteRoot(
        MutationCteNode node)
    {
        _cteRoots[_cteRootCount++] =
            node;
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
                toKeyValue,

                edgeKeyColumn,
                edgeKeyValue,

                GraphMergeKey.NormalizeProperties(
                    edgeProperties));


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



    public void ValidateGraphMerges()
    {
        var seen =
            new HashSet<GraphMergeKey>();


        for (var i = 0;
             i < _graphMergeCount;
             i++)
        {
            var merge =
                _graphMerges[i];


            var key =
                new GraphMergeKey(
                    merge.GraphName,
                    merge.EdgeLabel,

                    merge.FromLabel,
                    merge.FromKeyColumn,
                    merge.FromKeyValue,

                    merge.ToLabel,
                    merge.ToKeyColumn,
                    merge.ToKeyValue,

                    merge.EdgeKeyColumn,
                    merge.EdgeKeyValue,

                    GraphMergeKey.NormalizeProperties(
                        merge.EdgeProperties));


            if (!seen.Add(key))
            {
                throw new InvalidOperationException(
                    $"Duplicate graph merge detected: {merge.EdgeLabel}");
            }
        }
    }



    public MutationPlan Build()
    {
        var rows =
            ImmutableArray.CreateBuilder<UpsertRow>(
                _rowCount);


        for (var i = 0; i < _rowCount; i++)
        {
            rows.Add(_rows[i]);
        }



        var ctes =
            ImmutableArray.CreateBuilder<MutationCteNode>(
                _cteRootCount);


        for (var i = 0; i < _cteRootCount; i++)
        {
            ctes.Add(_cteRoots[i]);
        }



        var merges =
            ImmutableArray.CreateBuilder<GraphMergeSpec>(
                _graphMergeCount);


        for (var i = 0; i < _graphMergeCount; i++)
        {
            merges.Add(_graphMerges[i]);
        }



        var dependencies =
            ImmutableArray.CreateBuilder<MutationDependency>(
                _dependencyCount);


        for (var i = 0; i < _dependencyCount; i++)
        {
            dependencies.Add(
                _dependencies[i]);
        }



        return new MutationPlan(
            rows.ToImmutable(),
            ctes.ToImmutable(),
            merges.ToImmutable(),
            dependencies.ToImmutable());
    }
}