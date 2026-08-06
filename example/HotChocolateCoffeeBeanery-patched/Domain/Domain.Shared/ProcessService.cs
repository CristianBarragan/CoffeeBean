using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Foundation;
using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;
using CoffeeBeanery.GraphQL.Core.Runtime.Ordering;
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


    /// <summary>
    /// Real pagination through the Foundation pipeline. See
    /// ProcessService.QueryProcessAsyncViaFoundationPaged remarks --
    /// on the interface (unlike QueryProcessAsyncViaFoundation and
    /// MutationProcessAsyncViaFoundation, which are deliberately NOT on
    /// this interface, for manual direct-call comparison only) because
    /// WrapperQueryResolver needs to reach it through the DI-injected
    /// interface, not the concrete class.
    /// </summary>
    Task<QueryResult<M>> QueryProcessAsyncViaFoundationPaged(
        string cacheKey,
        ISelection selection,
        string modelName,
        int? first,
        string? after,
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
            var context =
                new FilterCompilationContext(queryPlan.RootStorageEntityId);

            var whereSql =
                FilterSqlWriter.Write(
                    filter,
                    queryPlan.RootStorageEntityId,
                    queryPlan.RootAlias,
                    context);

            sql =
                sql + " WHERE " + whereSql;

            models =
                await ExecuteAndMaterializeAsync(
                    sql,
                    context.Parameters,
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



    /// <summary>
    /// Real, working pagination through the Foundation pipeline -- unlike
    /// the schema-shape-only [UsePaging] currently on WrapperQueryResolver
    /// (which always fetches and returns the ENTIRE result set; see remarks
    /// there), this actually pushes ORDER BY/LIMIT/a keyset predicate to
    /// SQL and computes real HasNextPage/cursors from what was actually
    /// fetched.
    ///
    /// Keyset (not offset) pagination, always seeking by the user's `order`
    /// terms (if any) plus ModelMetadata.PrimaryKey as a tiebreaker -- see
    /// OrderSqlWriter remarks for the compound-cursor scheme and its
    /// single-direction-only limitation. With no `order` argument, this is
    /// equivalent to ordering/seeking by PrimaryKey alone. Chosen over
    /// offset pagination specifically because offset drifts (skips/
    /// duplicates rows) under concurrent writes, a real correctness concern
    /// on live banking data. A model with no PrimaryKey cannot be paginated
    /// this way and throws clearly rather than silently falling back to
    /// something else.
    ///
    /// Filtering (the `where` argument) composes with this exactly as in
    /// QueryProcessAsyncViaFoundation, except it now resolves against the
    /// full navigable entity graph (RuntimeEntityMetadataRegistry.GetGraph)
    /// rather than just the root entity -- navigation FILTER RESOLUTION
    /// works, but FilterSqlWriter still only writes SQL for root-entity
    /// fields (navigation filter SQL writing is a separate, not-yet-built
    /// piece; see FilterSqlWriter remarks). Ordering by a navigation field
    /// works today because OrderSqlWriter resolves aliases directly rather
    /// than going through FilterSqlWriter's root-only restriction.
    /// </summary>
    public async Task<QueryResult<TResult>> QueryProcessAsyncViaFoundationPaged(
        string cacheKey,
        ISelection selection,
        string modelName,
        int? first,
        string? after,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

        var rootOutputAlias =
            modelName;

        var model =
            GeneratedMetadata.GetModel(rootEntityId);

        if (model.PrimaryKey is null)
        {
            throw new NotSupportedException(
                $"Model '{model.Name}' has no PrimaryKey -- cannot paginate. " +
                "See ModelMetadata.PrimaryKey / IdEmitter.EmitModelMetadata remarks.");
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

        var entityGraph =
            RuntimeEntityMetadataRegistry.GetGraph(rootEntityId);

        var orderArg =
            selection.SyntaxNode.Arguments
                .FirstOrDefault(a =>
                    string.Equals(
                        a.Name.Value,
                        "order",
                        StringComparison.OrdinalIgnoreCase));

        var orderTerms =
            OrderCompiler.Compile(orderArg?.Value);

        var orderFieldTerms =
            OrderSqlWriter.ResolveFields(
                orderTerms,
                entityGraph,
                rootEntityId);

        var queryNode =
            PlannerRegistry.BuildQueryNode(
                rootEntityId,
                selectionIr,
                isRoot: true);

        var columnsToForce =
            new List<ColumnReference> { model.PrimaryKey };

        foreach (var (field, _) in orderFieldTerms)
        {
            columnsToForce.Add(
                new ColumnReference(
                    GeneratedMetadata.GetEntity(field.StorageEntityId),
                    field.ColumnId));
        }

        queryNode =
            PagingSqlWriter.EnsureColumnsSelected(
                queryNode,
                columnsToForce);

        var queryPlan =
            QueryPlanTranslator.FromQueryNode(queryNode);

        var resolvedOrderTerms =
            OrderSqlWriter.ResolveAliases(
                orderFieldTerms,
                queryPlan);

        var baseSql =
            _sqlWriter.WriteSelect(queryPlan);

        var pkColumnName =
            PagingSqlWriter.ResolvePrimaryKeyColumnName(
                model.PrimaryKey);

        var context =
            new FilterCompilationContext(
                queryPlan.RootStorageEntityId);

        var predicates =
            new List<string>();

        var filter =
            FilterQueryExtension.CompileWhere(
                selection,
                rootEntityId,
                new FilterMetadataResolver(entityGraph));

        if (filter != null)
        {
            predicates.Add(
                FilterSqlWriter.Write(
                    filter,
                    queryPlan.RootStorageEntityId,
                    queryPlan.RootAlias,
                    context));
        }

        var seekPredicate =
            OrderSqlWriter.BuildSeekPredicate(
                resolvedOrderTerms,
                queryPlan.RootAlias,
                pkColumnName,
                after,
                context);

        if (seekPredicate != null)
            predicates.Add(seekPredicate);

        var whereSql =
            predicates.Count > 0
                ? " WHERE " + string.Join(" AND ", predicates)
                : string.Empty;

        var countSql =
            $"SELECT COUNT(*) FROM ({baseSql}{whereSql}) AS __count";

        // Default page size when the client asks for no explicit `first` --
        // an unbounded fetch defeats the whole point of paginating.
        var pageSize =
            first ?? 50;

        var orderBy =
            OrderSqlWriter.BuildOrderByClause(
                resolvedOrderTerms,
                queryPlan.RootAlias,
                pkColumnName);

        // Fetch one extra row past the page size -- a standard trick to
        // determine HasNextPage from a single query instead of a second
        // round-trip: if pageSize+1 rows come back, there's a next page,
        // and the extra row is trimmed before returning.
        var pagedSql =
            $"{baseSql}{whereSql} {orderBy} LIMIT {pageSize + 1}";

        var (models, cursors, hasNextPage) =
            await ExecutePagedAndMaterializeAsync(
                pagedSql,
                context.Parameters,
                rootEntityId,
                queryPlan,
                pkColumnName,
                resolvedOrderTerms,
                pageSize,
                cancellationToken);

        var totalCount =
            await ExecuteCountAsync(
                countSql,
                context.Parameters,
                cancellationToken);

        var results =
            _wrap(models);

        return new QueryResult<TResult>
        {
            Models = results,
            Cursors = cursors,
            TotalCount = totalCount,
            TotalPageRecords = results.Count,
            HasNextPage = hasNextPage,
            HasPreviousPage = !string.IsNullOrEmpty(after),
            StartCursor = cursors.Count > 0 ? cursors[0] : null,
            EndCursor = cursors.Count > 0 ? cursors[^1] : null
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



    /// <summary>
    /// Parameterized + cursor-extracting variant, for
    /// QueryProcessAsyncViaFoundationPaged. Reads every resolved order
    /// term's column plus the primary key directly off each row (via the
    /// DbDataReader, before/alongside materializing that row) to build a
    /// real per-row compound cursor via OrderSqlWriter.EncodeCompoundCursor
    /// -- this works regardless of whether the client itself selected
    /// those fields, because the caller already forced them into the
    /// projection via PagingSqlWriter.EnsureColumnsSelected. With no order
    /// terms, this still produces a valid (single-element) compound cursor
    /// containing just the primary key. Trims the pageSize+1'th row
    /// (fetched only to determine HasNextPage) before returning.
    /// </summary>
    private async Task<(List<TModel> Models, List<string> Cursors, bool HasNextPage)>
        ExecutePagedAndMaterializeAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        ushort rootEntityId,
        QueryPlan queryPlan,
        string primaryKeyColumnName,
        IReadOnlyList<ResolvedOrderTerm> orderTerms,
        int pageSize,
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

        for (var i = 0; i < layout.Segments.Length; i++)
        {
            var segment =
                layout.Segments[i];

            var columnCount =
                _meta.EntityColumnName[segment.StorageEntityId]
                    .Length;

            segmentMaps[i] =
                queryPlan.BuildColumnMap(
                    segment.StorageEntityId,
                    segment.EntityOutputAlias,
                    (ushort)columnCount);
        }

        // Cursor columns: every order term's column, then the primary key
        // last -- same order BuildOrderByClause/BuildSeekPredicate use, so
        // the encoded values line up with the row-comparison predicate.
        var cursorOrdinals =
            new int[orderTerms.Count + 1];

        for (var i = 0; i < orderTerms.Count; i++)
        {
            cursorOrdinals[i] =
                reader.GetOrdinal(orderTerms[i].ColumnName);
        }

        cursorOrdinals[orderTerms.Count] =
            reader.GetOrdinal(primaryKeyColumnName);

        var rowMatrix =
            new List<object?[]>();

        var cursors =
            new List<string>();

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

            var cursorValues =
                new object?[cursorOrdinals.Length];

            for (var i = 0; i < cursorOrdinals.Length; i++)
            {
                cursorValues[i] =
                    reader.IsDBNull(cursorOrdinals[i])
                        ? null
                        : reader.GetValue(cursorOrdinals[i]);
            }

            cursors.Add(
                OrderSqlWriter.EncodeCompoundCursor(cursorValues));
        }

        var hasNextPage =
            rowMatrix.Count > pageSize;

        if (hasNextPage)
        {
            rowMatrix.RemoveAt(rowMatrix.Count - 1);
            cursors.RemoveAt(cursors.Count - 1);
        }

        var models =
            ResultBuilderRegistry.Build<TModel>(
                rootEntityId,
                layout,
                rowMatrix);

        return (models, cursors, hasNextPage);
    }

    /// <summary>
    /// Runs a `SELECT COUNT(*) FROM (...) AS __count` wrapping the same
    /// base SELECT + WHERE the paged query uses (minus ORDER BY/LIMIT),
    /// with the same parameters bound. A real count, not the size of
    /// whatever happened to be fetched.
    /// </summary>
    private async Task<int> ExecuteCountAsync(
        string countSql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
    {
        await using var connection =
            await AgeConnectionFactory.OpenAsync(_dataSource);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            countSql;

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

        var result =
            await command.ExecuteScalarAsync(ct);

        return
            result is null || result is DBNull
                ? 0
                : Convert.ToInt32(result);
    }
}