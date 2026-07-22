using System;
using System.Collections.Generic;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using FASTER.core;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using Npgsql;

namespace Domain.Shared;


public interface IProcessService<M>
    where M : class
{
    Task<QueryResult<M>> QueryProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken);


    Task<QueryResult<M>> MutationProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken);
}



public sealed class ProcessService<TModel, TResult> :
    IProcessService<TResult>
    where TModel : class
    where TResult : class
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly IFasterKV<string,string> _cache;

    private readonly AdapterLookup _adapterLookup;

    private readonly IEntityMetaProvider _meta;

    private readonly PostgresSqlWriter _sqlWriter;

    private readonly IReadOnlyList<IQueryPlanContributor> _queryContributors;

    private readonly IReadOnlyList<IMutationPlanContributor> _mutationContributors;

    private readonly IPlannerRegistry _plannerRegistry;

    private readonly Func<List<TModel>, List<TResult>> _wrap;



    public ProcessService(
        NpgsqlDataSource dataSource,
        IFasterKV<string,string> cache,
        AdapterLookup adapterLookup,
        IEntityMetaProvider meta,
        PostgresSqlWriter sqlWriter,
        IPlannerRegistry plannerRegistry,
        Func<List<TModel>,List<TResult>> wrap,
        IEnumerable<IQueryPlanContributor>? queryContributors = null,
        IEnumerable<IMutationPlanContributor>? mutationContributors = null)
    {
        _dataSource = dataSource;
        _cache = cache;
        _adapterLookup = adapterLookup;
        _meta = meta;
        _sqlWriter = sqlWriter;
        _plannerRegistry = plannerRegistry;
        _wrap = wrap;

        _queryContributors =
            queryContributors?.ToArray()
            ?? Array.Empty<IQueryPlanContributor>();

        _mutationContributors =
            mutationContributors?.ToArray()
            ?? Array.Empty<IMutationPlanContributor>();
    }
    

    public async Task<QueryResult<TResult>> MutationProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

        var rootStorageEntityId =
            ResolveRootStorageEntityId(modelName);

        var rootOutputAlias =
            modelName;


        MutationPlan? mutationPlan = null;


        var mutationArg =
            selection.SyntaxNode.Arguments
                .FirstOrDefault(a =>
                    !string.Equals(a.Name.Value, "where",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(a.Name.Value, "order",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(a.Name.Value, "first",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(a.Name.Value, "last",
                        StringComparison.OrdinalIgnoreCase));


        if (mutationArg?.Value is ObjectValueNode wrapperObj)
        {
            var entityFieldName =
                char.ToLowerInvariant(rootOutputAlias[0])
                + rootOutputAlias.Substring(1);


            var entityField =
                wrapperObj.Fields.FirstOrDefault(f =>
                    string.Equals(
                        f.Name.Value,
                        entityFieldName,
                        StringComparison.OrdinalIgnoreCase));


            var mutations =
                new List<MutationIR>();


            switch (entityField?.Value)
            {
                case ObjectValueNode obj:

                    mutations.Add(
                        HotChocolateAdapter.AdaptMutation(
                            rootEntityId,
                            rootOutputAlias,
                            obj,
                            _adapterLookup));

                    break;


                case ListValueNode list:

                    foreach (var item in list.Items)
                    {
                        if (item is ObjectValueNode itemObj)
                        {
                            mutations.Add(
                                HotChocolateAdapter.AdaptMutation(
                                    rootEntityId,
                                    rootOutputAlias,
                                    itemObj,
                                    _adapterLookup));
                        }
                    }

                    break;
            }


            if (mutations.Count > 0)
            {
                var builder =
                    new MutationPlanBuilder();


                foreach (var mutation in mutations)
                {
                    var optimized =
                        MutationOptimizer.Optimize(mutation);


                    if (!MutationOptimizer.HasWork(optimized))
                        continue;


                    MutationRuntimePlanner.Build(
                        rootEntityId,
                        optimized,
                        ref builder);


                    foreach (var contributor in _mutationContributors)
                    {
                        contributor.Contribute(
                            rootEntityId,
                            optimized,
                            ref builder);
                    }
                }


                mutationPlan =
                    builder.Build();
            }
        }
        
                var selectionSet =
            selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException(
                "Selection has no SelectionSet.");


        var selectionIr =
            HotChocolateAdapter.AdaptQuery(
                rootEntityId,
                rootOutputAlias,
                selectionSet,
                _adapterLookup);


        selectionIr =
            SelectionOptimizer.Optimize(selectionIr);



        var queryBuilder =
            new QueryPlanBuilder();


        queryBuilder.SetRoot(
            rootEntityId,
            rootStorageEntityId,
            rootOutputAlias);



        _plannerRegistry.Build(
            rootEntityId,
            selectionIr,
            ref queryBuilder);



        foreach (var contributor in _queryContributors)
        {
            contributor.Contribute(
                rootEntityId,
                selectionIr,
                ref queryBuilder);
        }


        var queryPlan =
            queryBuilder.Build();



        var sqlParts =
            new List<string>();


        if (mutationPlan.HasValue)
        {
            var upsert =
                _sqlWriter.WriteUpserts(
                    mutationPlan.Value);


            var graph =
                _sqlWriter.WriteGraphMerges(
                    mutationPlan.Value);


            if (!string.IsNullOrWhiteSpace(upsert))
                sqlParts.Add(upsert);


            if (!string.IsNullOrWhiteSpace(graph))
                sqlParts.Add(graph);
        }


        sqlParts.Add(
            _sqlWriter.WriteSelect(queryPlan));


        var sql =
            string.Join(";", sqlParts);


        var models =
            await ExecuteAndMaterializeAsync(
                sql,
                rootEntityId,
                queryPlan,
                cancellationToken);



        var results =
            _wrap(models);



        return new QueryResult<TResult>
        {
            Models = results,
            TotalCount = results.Count,
            TotalPageRecords = results.Count
        };
    }
    

    public async Task<QueryResult<TResult>> QueryProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

        var rootStorageEntityId =
            ResolveRootStorageEntityId(modelName);

        var rootOutputAlias =
            modelName;


        var selectionSet =
            selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException(
                "Selection has no SelectionSet.");



        var selectionIr =
            HotChocolateAdapter.AdaptQuery(
                rootEntityId,
                rootOutputAlias,
                selectionSet,
                _adapterLookup);



        selectionIr =
            SelectionOptimizer.Optimize(selectionIr);



        var builder =
            new QueryPlanBuilder();


        builder.SetRoot(
            rootEntityId,
            rootStorageEntityId,
            rootOutputAlias);



        _plannerRegistry.Build(
            rootEntityId,
            selectionIr,
            ref builder);



        foreach (var contributor in _queryContributors)
        {
            contributor.Contribute(
                rootEntityId,
                selectionIr,
                ref builder);
        }



        var queryPlan =
            builder.Build();



        var sql =
            _sqlWriter.WriteSelect(queryPlan);



        var models =
            await ExecuteAndMaterializeAsync(
                sql,
                rootEntityId,
                queryPlan,
                cancellationToken);



        var results =
            _wrap(models);



        return new QueryResult<TResult>
        {
            Models = results,
            TotalCount = results.Count,
            TotalPageRecords = results.Count
        };
    }
    
        private async Task<List<TModel>> ExecuteAndMaterializeAsync(
        string sql,
        ushort rootEntityId,
        QueryPlan queryPlan,
        CancellationToken ct)
    {
        await using var connection =
            await AgeConnectionFactory.OpenAsync(_dataSource);


        await using var command =
            connection.CreateCommand();


        command.CommandText = sql;



        await using var reader =
            await command.ExecuteReaderAsync(ct);



        // Skip mutation result sets.
        while (reader.FieldCount == 0)
        {
            if (!await reader.NextResultAsync(ct))
            {
                throw new InvalidOperationException(
                    "No SELECT result set returned.");
            }
        }



        var layout =
            RowLayout.FromQueryPlan(queryPlan);



        var segmentMaps =
            new ushort[layout.Segments.Length][];



        for (var i = 0; i < layout.Segments.Length; i++)
        {
            var segment =
                layout.Segments[i];


            var columnCount =
                _meta.EntityColumnName[
                    segment.StorageEntityId]
                    .Length;



            segmentMaps[i] =
                queryPlan.BuildColumnMap(
                    segment.StorageEntityId,
                    segment.EntityOutputAlias,
                    (ushort)columnCount);
        }



        var rowMatrix =
            new List<object?[]>();



        while (await reader.ReadAsync(ct))
        {
            var row =
                new object?[layout.Segments.Length];



            for (var i = 0; i < layout.Segments.Length; i++)
            {
                var segment =
                    layout.Segments[i];


                row[i] =
                    MaterializerRegistry.Materialize(
                        segment.StorageEntityId,
                        reader,
                        segmentMaps[i]);
            }


            rowMatrix.Add(row);
        }



        return ResultBuilderRegistry.Build<TModel>(
            rootEntityId,
            layout,
            rowMatrix);
    }
    

    private ushort ResolveRootEntityId(
        string modelName)
    {
        for (ushort i = 0; i < _meta.ModelName.Length; i++)
        {
            if (string.Equals(
                    _meta.ModelName[i][0],
                    modelName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }


        throw new InvalidOperationException(
            $"Unknown model '{modelName}'.");
    }



    private ushort ResolveRootStorageEntityId(
        string modelName)
    {
        if (!_meta.TryGetEntityId(
                modelName,
                out var entityId))
        {
            throw new InvalidOperationException(
                $"Unknown model '{modelName}'.");
        }



        var table =
            _meta.Table[entityId][0];



        for (ushort i = 0;
             i < _meta.EntityTable.Length;
             i++)
        {
            if (string.Equals(
                    _meta.EntityTable[i],
                    table,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }



        throw new InvalidOperationException(
            $"No storage entity found for '{modelName}'.");
    }
}



public interface IQueryPlanContributor
{
    void Contribute(
        ushort entityId,
        in SelectionIR selection,
        ref QueryPlanBuilder builder);
}



public interface IMutationPlanContributor
{
    void Contribute(
        ushort entityId,
        in MutationIR mutation,
        ref MutationPlanBuilder builder);
}