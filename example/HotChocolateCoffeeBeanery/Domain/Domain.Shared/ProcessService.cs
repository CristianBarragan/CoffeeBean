using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;
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

public sealed class ProcessService<TModel, TResult> :
    IProcessService<TResult>
    where TModel : class
    where TResult : class
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly IFasterKV<string, string> _cache;

    private readonly AdapterLookup _adapterLookup;

    private readonly IEntityMetaProvider _meta;

    private readonly PostgresSqlWriter _sqlWriter;

    private readonly IReadOnlyList<IQueryPlanContributor> _queryContributors;

    private readonly IReadOnlyList<IMutationPlanContributor> _mutationContributors;

    private readonly IPlannerRegistry _plannerRegistry;

    private readonly Func<List<TModel>, List<TResult>> _wrap;



    public ProcessService(
        NpgsqlDataSource dataSource,
        IFasterKV<string, string> cache,
        AdapterLookup adapterLookup,
        IEntityMetaProvider meta,
        PostgresSqlWriter sqlWriter,
        IPlannerRegistry plannerRegistry,
        Func<List<TModel>, List<TResult>> wrap,
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
                    !string.Equals(
                        a.Name.Value,
                        "where",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "order",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "first",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "last",
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
            ref queryBuilder,
            isRoot: true);



        foreach (var contributor in _queryContributors)
        {
            contributor.Contribute(
                rootEntityId,
                selectionIr,
                ref queryBuilder);
        }



        var queryPlan =
            queryBuilder.Build();



        var sql =
            mutationPlan.HasValue
                ? _sqlWriter.WriteUpsertThenSelect(
                    mutationPlan.Value,
                    queryPlan)
                : _sqlWriter.WriteSelect(
                    queryPlan);



        var models =
            await ExecuteMutationAndMaterializeAsync(
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
            ref builder,
            isRoot: true);



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



    /// <summary>
    /// Opt-in parallel path through the new Foundation/QueryNode pipeline
    /// (PlannerRegistry.BuildQueryNode -> QueryPlanTranslator.FromQueryNode),
    /// instead of the old imperative _plannerRegistry.Build(ref QueryPlanBuilder)
    /// path QueryProcessAsync above uses. Not wired into IProcessService or
    /// called from anywhere yet -- call this directly to compare its SQL/
    /// results against QueryProcessAsync for the same query while validating
    /// the new pipeline.
    ///
    /// Filtering (the `where` argument) IS wired in here, through
    /// FilterQueryExtension/WhereCompiler/FilterMetadataResolver (which
    /// already existed but had no SQL-writing consumer) -> FilterSqlWriter
    /// (new). SCOPE: only fields on the query's root entity, only
    /// eq/neq/in -- navigation filters (`customer: {...}`), collection
    /// filters (`some`/`all`/`none`), and filtering on a joined/composite
    /// entity's own fields all throw a clear NotSupportedException rather
    /// than silently producing wrong SQL. See RuntimeEntityMetadataRegistry
    /// and FilterSqlWriter remarks for why.
    ///
    /// KNOWN LIMITATIONS:
    /// - Ordering (`order`) and pagination (`first`/`last`/`after`/`before`)
    ///   are not implemented at all yet -- neither here nor in the old
    ///   QueryProcessAsync path (DynamicSortModule, the type the `order`
    ///   argument is declared against, doesn't exist in this codebase --
    ///   that's a pre-existing gap, not something this change introduced).
    /// - _queryContributors (the old builder's extension point) do NOT run
    ///   here -- they're tied to `ref QueryPlanBuilder` and haven't been
    ///   ported to QueryNode.
    /// </summary>
    public async Task<QueryResult<TResult>> QueryProcessAsyncViaFoundation(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

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

        var queryNode =
            PlannerRegistry.BuildQueryNode(
                rootEntityId,
                selectionIr,
                isRoot: true);

        var queryPlan =
            QueryPlanTranslator.FromQueryNode(queryNode);

        var sql =
            _sqlWriter.WriteSelect(queryPlan);

        var filter =
            FilterQueryExtension.CompileWhere(
                selection,
                rootEntityId,
                new FilterMetadataResolver(
                    ImmutableArray.Create(
                        RuntimeEntityMetadataRegistry.GetRootOnly(
                            rootEntityId))));

        List<TModel> models;

        if (filter != null)
        {
            var (whereSql, parameters) =
                FilterSqlWriter.Write(
                    filter,
                    queryPlan.RootStorageEntityId,
                    queryPlan.RootAlias);

            sql =
                sql + " WHERE " + whereSql;

            models =
                await ExecuteAndMaterializeAsync(
                    sql,
                    parameters,
                    rootEntityId,
                    queryPlan,
                    cancellationToken);
        }
        else
        {
            models =
                await ExecuteAndMaterializeAsync(
                    sql,
                    rootEntityId,
                    queryPlan,
                    cancellationToken);
        }

        var results =
            _wrap(models);

        return new QueryResult<TResult>
        {
            Models = results,
            TotalCount = results.Count,
            TotalPageRecords = results.Count
        };
    }



    /// <summary>
    /// Opt-in parallel path through the new Foundation/MutationOperation
    /// pipeline, alongside QueryProcessAsyncViaFoundation. Mirrors
    /// MutationProcessAsync's argument-parsing and query-building exactly,
    /// but replaces the mutation-writing half (MutationRuntimePlanner.Build
    /// into a ref MutationPlanBuilder) with
    /// MutationOperationBuilder.Build -> MutationPlanTranslator.FromMutationOperations.
    /// Not wired into IProcessService or called from anywhere yet.
    ///
    /// SCOPE (same boundary MutationOperationBuilder itself documents):
    /// only simple, single-row, non-interceptor mutations. A mutation that
    /// needs CTE-dependency resolution (a child referencing a parent's
    /// generated surrogate id), a graph merge (MutationKind.GraphEdge --
    /// e.g. CustomerCustomerEdge), or the Materializer/Interceptor/
    /// Dematerializer registries will silently produce incomplete SQL
    /// through this path. Compare against MutationProcessAsync's output
    /// for the same mutation before trusting this for anything beyond a
    /// flat single-entity create/update.
    /// </summary>
    public async Task<QueryResult<TResult>> MutationProcessAsyncViaFoundation(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

        var rootOutputAlias =
            modelName;

        MutationPlan? mutationPlan = null;

        var mutationArg =
            selection.SyntaxNode.Arguments
                .FirstOrDefault(a =>
                    !string.Equals(
                        a.Name.Value,
                        "where",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "order",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "first",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        a.Name.Value,
                        "last",
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
                var operations =
                    new List<CoffeeBeanery.GraphQL.Core.Foundation.MutationPlan.MutationOperation>();

                var values =
                    new Dictionary<ushort, string?>();

                var metadata =
                    MutationMetadataRegistry.Get(rootEntityId);

                foreach (var mutation in mutations)
                {
                    var optimized =
                        MutationOptimizer.Optimize(mutation);

                    if (!MutationOptimizer.HasWork(optimized))
                        continue;

                    var built =
                        MutationOperationBuilder.Build(
                            optimized,
                            metadata);

                    operations.AddRange(built.Operations);

                    foreach (var kvp in built.Values)
                        values[kvp.Key] = kvp.Value;
                }

                if (operations.Count > 0)
                {
                    mutationPlan =
                        MutationPlanTranslator.FromMutationOperations(
                            operations,
                            fieldId =>
                                values.TryGetValue(fieldId, out var v)
                                    ? v
                                    : null);
                }
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

        var queryNode =
            PlannerRegistry.BuildQueryNode(
                rootEntityId,
                selectionIr,
                isRoot: true);

        var queryPlan =
            QueryPlanTranslator.FromQueryNode(queryNode);

        var sql =
            mutationPlan.HasValue
                ? _sqlWriter.WriteUpsertThenSelect(
                    mutationPlan.Value,
                    queryPlan)
                : _sqlWriter.WriteSelect(
                    queryPlan);

        var models =
            await ExecuteMutationAndMaterializeAsync(
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



    private async Task<List<TModel>> ExecuteMutationAndMaterializeAsync(
        string sql,
        ushort rootEntityId,
        QueryPlan queryPlan,
        CancellationToken ct)
    {
        await using var connection =
            await AgeConnectionFactory.OpenAsync(_dataSource);



        await using var transaction =
            await connection.BeginTransactionAsync(ct);



        try
        {
            await using var command =
                connection.CreateCommand();



            command.Transaction =
                transaction;



            command.CommandText =
                sql;



            await using var reader =
                await command.ExecuteReaderAsync(ct);



            while (reader.FieldCount == 0)
            {
                if (!await reader.NextResultAsync(ct))
                {
                    throw new InvalidOperationException(
                        "Mutation completed but no SELECT result returned.");
                }
            }



            var layout =
                RowLayout.FromQueryPlan(queryPlan);



            var segmentMaps =
                new ushort[layout.Segments.Length][];



            for (var i = 0;
                 i < layout.Segments.Length;
                 i++)
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



                for (var i = 0;
                     i < layout.Segments.Length;
                     i++)
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



            await reader.DisposeAsync();



            await transaction.CommitAsync(ct);



            return ResultBuilderRegistry.Build<TModel>(
                rootEntityId,
                layout,
                rowMatrix);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
    
        /// <summary>
        /// Parameterized overload, additive next to ExecuteAndMaterializeAsync
        /// above (which is unchanged and still used by every existing caller).
        /// Needed because that method has no parameter-binding at all --
        /// FilterSqlWriter always emits @pN placeholders rather than inlining
        /// filter values, and this is what actually binds them via
        /// DbCommand.Parameters rather than string concatenation.
        /// </summary>
        private async Task<List<TModel>> ExecuteAndMaterializeAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        ushort rootEntityId,
        QueryPlan queryPlan,
        CancellationToken ct)
    {
        await using var connection =
            await AgeConnectionFactory.OpenAsync(_dataSource);



        await using var command =
            connection.CreateCommand();



        command.CommandText =
            sql;



        foreach (var kvp in parameters)
        {
            var dbParameter =
                command.CreateParameter();

            dbParameter.ParameterName =
                kvp.Key;

            dbParameter.Value =
                kvp.Value ?? DBNull.Value;

            command.Parameters.Add(
                dbParameter);
        }



        await using var reader =
            await command.ExecuteReaderAsync(ct);



        var layout =
            RowLayout.FromQueryPlan(queryPlan);



        var segmentMaps =
            new ushort[layout.Segments.Length][];



        for (var i = 0;
             i < layout.Segments.Length;
             i++)
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



            for (var i = 0;
                 i < layout.Segments.Length;
                 i++)
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



        command.CommandText =
            sql;



        await using var reader =
            await command.ExecuteReaderAsync(ct);



        var layout =
            RowLayout.FromQueryPlan(queryPlan);



        var segmentMaps =
            new ushort[layout.Segments.Length][];



        for (var i = 0;
             i < layout.Segments.Length;
             i++)
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



            for (var i = 0;
                 i < layout.Segments.Length;
                 i++)
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
        for (ushort i = 0;
             i < _meta.ModelName.Length;
             i++)
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