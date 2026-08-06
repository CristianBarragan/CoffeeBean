using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using FASTER.core;
using Npgsql;

namespace Domain.Shared;


public interface IProcessService<M>
    where M : class
{
    /// <summary>
    /// Resolves a GraphQL model name (e.g. "CustomerCustomerEdge") to its
    /// runtime entity id. Exposed so the GraphQL layer can build a
    /// SelectionIR/EntityFilterMetadata (which need the entity id) BEFORE
    /// calling QueryProcessAsync/MutationProcessAsync/QueryProcessAsyncPaged
    /// -- the adapting from ISelection happens entirely on the caller's
    /// side now, not inside ProcessService.
    /// </summary>
    ushort ResolveRootEntityId(string modelName);

    Task<QueryResult<M>> QueryProcessAsync(
        string cacheKey,
        QueryRequest request,
        string modelName,
        CancellationToken cancellationToken);


    Task<QueryResult<M>> MutationProcessAsync(
        string cacheKey,
        MutationRequest request,
        string modelName,
        CancellationToken cancellationToken);


    /// <summary>
    /// Real pagination through the Foundation pipeline -- see
    /// ProcessService.QueryProcessAsyncViaFoundationPaged remarks. On the
    /// interface (unlike the old QueryProcessAsyncViaFoundation and
    /// MutationProcessAsyncViaFoundation experiments, which have been
    /// removed as dead code superseded by this) because WrapperQueryResolver
    /// needs to reach it through the DI-injected interface, not the
    /// concrete class.
    /// </summary>
    Task<QueryResult<M>> QueryProcessAsyncViaFoundationPaged(
        string cacheKey,
        PagedQueryRequest request,
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

    private readonly IEntityMetaProvider _meta;

    private readonly CoffeeBeanery.GraphQL.Core.Foundation.Metadata.IMetadataProvider _metadataProvider;

    private readonly PostgresSqlWriter _sqlWriter;

    private readonly IReadOnlyList<IQueryPlanContributor> _queryContributors;

    private readonly IReadOnlyList<IMutationPlanContributor> _mutationContributors;

    private readonly IPlannerRegistry _plannerRegistry;

    private readonly Func<List<TModel>, List<TResult>> _wrap;



    public ProcessService(
        NpgsqlDataSource dataSource,
        IFasterKV<string, string> cache,
        IEntityMetaProvider meta,
        PostgresSqlWriter sqlWriter,
        IPlannerRegistry plannerRegistry,
        Func<List<TModel>, List<TResult>> wrap,
        IEnumerable<IQueryPlanContributor>? queryContributors = null,
        IEnumerable<IMutationPlanContributor>? mutationContributors = null,
        CoffeeBeanery.GraphQL.Core.Foundation.Metadata.IMetadataProvider? metadataProvider = null)
    {
        _dataSource = dataSource;
        _cache = cache;
        _meta = meta;
        _sqlWriter = sqlWriter;
        _plannerRegistry = plannerRegistry;
        _wrap = wrap;

        _metadataProvider =
            metadataProvider
            ?? CoffeeBeanery.GraphQL.Core.Foundation.GeneratedMetadataProvider.Instance;

        _queryContributors =
            queryContributors?.ToArray()
            ?? Array.Empty<IQueryPlanContributor>();

        _mutationContributors =
            mutationContributors?.ToArray()
            ?? Array.Empty<IMutationPlanContributor>();
    }



    public async Task<QueryResult<TResult>> MutationProcessAsync(
        string cacheKey,
        MutationRequest request,
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


        if (request.Mutations.Count > 0)
        {
            var builder =
                new MutationPlanBuilder();



            foreach (var mutation in request.Mutations)
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



        var selectionIr =
            SelectionOptimizer.Optimize(request.SelectionIr);



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
        QueryRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);


        var rootStorageEntityId =
            ResolveRootStorageEntityId(modelName);


        var rootOutputAlias =
            modelName;



        var selectionIr =
            SelectionOptimizer.Optimize(request.SelectionIr);



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


        List<TModel> models;

        if (request.Filter != null)
        {
            var context =
                new FilterCompilationContext(queryPlan.RootStorageEntityId);

            var whereSql =
                FilterSqlWriter.Write(
                    request.Filter,
                    queryPlan.RootStorageEntityId,
                    queryPlan.RootAlias,
                    context,
                    _metadataProvider);

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
    /// Real, working pagination through the Foundation pipeline -- unlike
    /// the schema-shape-only [UsePaging] currently on WrapperQueryResolver
    /// (which always fetches and returns the ENTIRE result set; see remarks
    /// there), this actually pushes ORDER BY/LIMIT/a keyset predicate to
    /// SQL and computes real HasNextPage/cursors from what was actually
    /// fetched.
    ///
    /// Keyset (not offset) pagination: orders and seeks by
    /// ModelMetadata.PrimaryKey (see IdEmitter.EmitModelMetadata remarks),
    /// not a client-facing sort -- chosen over offset specifically because
    /// offset drifts (skips/duplicates rows) under concurrent writes, a
    /// real correctness concern on live banking data. A model with no
    /// PrimaryKey cannot be paginated this way and throws clearly rather
    /// than silently falling back to something else.
    ///
    /// Filtering (the `where` argument) composes with this exactly as in
    /// QueryProcessAsyncViaFoundation -- same scope limits apply (root
    /// entity fields only, eq/neq/in only).
    /// </summary>
    public async Task<QueryResult<TResult>> QueryProcessAsyncViaFoundationPaged(
        string cacheKey,
        PagedQueryRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId =
            ResolveRootEntityId(modelName);

        var rootOutputAlias =
            modelName;

        var model =
            _metadataProvider.GetModel(rootEntityId);

        if (model.PrimaryKey is null)
        {
            throw new NotSupportedException(
                $"Model '{model.Name}' has no PrimaryKey -- cannot paginate. " +
                "See ModelMetadata.PrimaryKey / IdEmitter.EmitModelMetadata remarks.");
        }

        var selectionIr =
            SelectionOptimizer.Optimize(request.SelectionIr);

        var queryNode =
            GeneratedPlanners.BuildQueryNode(
                rootEntityId,
                selectionIr,
                isRoot: true);

        queryNode =
            PagingSqlWriter.EnsurePrimaryKeySelected(
                queryNode,
                model.PrimaryKey);

        var queryPlan =
            QueryPlanTranslator.FromQueryNode(queryNode);

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

        if (request.Filter != null)
        {
            predicates.Add(
                FilterSqlWriter.Write(
                    request.Filter,
                    queryPlan.RootStorageEntityId,
                    queryPlan.RootAlias,
                    context,
                    _metadataProvider));
        }

        var afterPredicate =
            PagingSqlWriter.BuildAfterPredicate(
                queryPlan.RootAlias,
                pkColumnName,
                request.After,
                context);

        if (afterPredicate != null)
            predicates.Add(afterPredicate);

        var whereSql =
            predicates.Count > 0
                ? " WHERE " + string.Join(" AND ", predicates)
                : string.Empty;

        var countSql =
            $"SELECT COUNT(*) FROM ({baseSql}{whereSql}) AS __count";

        // Default page size when the client asks for no explicit `first` --
        // an unbounded fetch defeats the whole point of paginating.
        var pageSize =
            request.First ?? 50;

        var orderBy =
            PagingSqlWriter.BuildOrderBy(
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
            HasPreviousPage = !string.IsNullOrEmpty(request.After),
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



    public ushort ResolveRootEntityId(
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
    /// QueryProcessAsyncViaFoundationPaged. Reads pkColumnName directly off
    /// each row (via the DbDataReader, before/alongside materializing that
    /// row) to build a real per-row cursor -- this works regardless of
    /// whether the client itself selected that field, because
    /// PagingSqlWriter.EnsurePrimaryKeySelected already guaranteed it's in
    /// the SELECT list. Trims the pageSize+1'th row (fetched only to
    /// determine HasNextPage) before returning.
    /// </summary>
    private async Task<(List<TModel> Models, List<string> Cursors, bool HasNextPage)>
        ExecutePagedAndMaterializeAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        ushort rootEntityId,
        QueryPlan queryPlan,
        string primaryKeyColumnName,
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

        var pkOrdinal =
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

            var pkValue =
                reader.IsDBNull(pkOrdinal)
                    ? null
                    : reader.GetValue(pkOrdinal);

            cursors.Add(
                pkValue is null
                    ? string.Empty
                    : PagingSqlWriter.EncodeCursor(pkValue));
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