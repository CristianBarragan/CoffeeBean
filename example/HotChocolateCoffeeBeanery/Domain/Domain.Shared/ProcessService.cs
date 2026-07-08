using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using FASTER.core;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using Npgsql;

namespace Domain.Shared;

public interface IProcessService<M> where M : class
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

public class ProcessService<TModel, TResult> : IProcessService<TResult>
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
        _queryContributors = queryContributors?.ToArray() ?? Array.Empty<IQueryPlanContributor>();
        _mutationContributors = mutationContributors?.ToArray() ?? Array.Empty<IMutationPlanContributor>();
    }

    public async Task<QueryResult<TResult>> MutationProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId        = ResolveRootEntityId(modelName);
        var rootStorageEntityId = ResolveRootStorageEntityId(modelName);
        var rootOutputAlias     = modelName;

        var mutationArg = selection.SyntaxNode.Arguments
            .FirstOrDefault(a =>
                !string.Equals(a.Name.Value, "where",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "order",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "first",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "last",   StringComparison.OrdinalIgnoreCase));

        MutationPlan? mutationPlan = null;

        if (mutationArg?.Value is ObjectValueNode inputObj)
        {
            var mutationIr = HotChocolateAdapter.AdaptMutation(
                rootEntityId,
                rootOutputAlias,
                inputObj,
                _adapterLookup);

            mutationIr = MutationOptimizer.Optimize(mutationIr);

            if (MutationOptimizer.HasWork(mutationIr))
            {
                var mutationPlanBuilder = new MutationPlanBuilder();

                _plannerRegistry.BuildMutation(
                    rootEntityId,
                    mutationIr,
                    ref mutationPlanBuilder);

                foreach (var contributor in _mutationContributors)
                    contributor.Contribute(
                        rootEntityId,
                        mutationIr,
                        ref mutationPlanBuilder);

                mutationPlan = mutationPlanBuilder.Build();
            }
        }

        var selectionSet = selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException("Selection has no SelectionSet.");

        var selectionIr = HotChocolateAdapter.AdaptQuery(
            rootEntityId, rootOutputAlias, selectionSet, _adapterLookup);

        selectionIr = SelectionOptimizer.Optimize(selectionIr);

        var queryPlanBuilder = new QueryPlanBuilder();
        queryPlanBuilder.SetRoot(rootEntityId, rootStorageEntityId, rootOutputAlias);

        _plannerRegistry.Build(rootEntityId, selectionIr, ref queryPlanBuilder);

        foreach (var contributor in _queryContributors)
            contributor.Contribute(rootEntityId, selectionIr, ref queryPlanBuilder);

        var queryPlan = queryPlanBuilder.Build();

        var upsertSql = mutationPlan is not null ? _sqlWriter.WriteUpserts(mutationPlan.Value) : "";
        var selectSql = _sqlWriter.WriteSelect(queryPlan);
        var finalSql  = string.IsNullOrEmpty(upsertSql) ? selectSql : upsertSql + ";" + selectSql;

        var models  = await ExecuteAndMaterializeAsync(finalSql, rootEntityId, queryPlan, cancellationToken);
        var results = _wrap(models);

        return new QueryResult<TResult>
        {
            Models           = results,
            TotalCount       = results.Count,
            TotalPageRecords = results.Count
        };
    }

    public async Task<QueryResult<TResult>> QueryProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId        = ResolveRootEntityId(modelName);
        var rootStorageEntityId = ResolveRootStorageEntityId(modelName);
        var rootOutputAlias     = modelName;

        var selectionSet = selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException("Selection has no SelectionSet.");

        var selectionIr = HotChocolateAdapter.AdaptQuery(
            rootEntityId, rootOutputAlias, selectionSet, _adapterLookup);

        selectionIr = SelectionOptimizer.Optimize(selectionIr);

        var queryPlanBuilder = new QueryPlanBuilder();
        queryPlanBuilder.SetRoot(rootEntityId, rootStorageEntityId, rootOutputAlias);

        _plannerRegistry.Build(rootEntityId, selectionIr, ref queryPlanBuilder);

        foreach (var contributor in _queryContributors)
            contributor.Contribute(rootEntityId, selectionIr, ref queryPlanBuilder);

        var queryPlan = queryPlanBuilder.Build();
        var sql       = _sqlWriter.WriteSelect(queryPlan);

        var models  = await ExecuteAndMaterializeAsync(sql, rootEntityId, queryPlan, cancellationToken);
        var results = _wrap(models);

        return new QueryResult<TResult>
        {
            Models           = results,
            TotalCount       = results.Count,
            TotalPageRecords = results.Count
        };
    }

    // ---------------------------------------------------------------
    // Raw ADO.NET execution + AOT-safe materialization
    // Always builds List<TModel>. Wrapping to TResult is the caller's job.
    // ---------------------------------------------------------------

    private async Task<List<TModel>> ExecuteAndMaterializeAsync(
        string sql,
        ushort rootEntityId,
        QueryPlan queryPlan,
        CancellationToken ct)
    {
        await using var connection = await AgeConnectionFactory.OpenAsync(_dataSource);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Skip result sets from upsert statements (no RETURNING -> FieldCount == 0).
        while (reader.FieldCount == 0)
        {
            if (!await reader.NextResultAsync(ct))
                throw new InvalidOperationException("Expected a SELECT result set but none was found.");
        }

        var layout = RowLayout.FromQueryPlan(queryPlan);

        var segmentMaps = new ushort[layout.Segments.Length][];
        for (int s = 0; s < layout.Segments.Length; s++)
        {
            var seg         = layout.Segments[s];
            var columnCount = _meta.EntityColumnName[seg.StorageEntityId].Length;
            segmentMaps[s]  = queryPlan.BuildColumnMap(
                seg.StorageEntityId,
                seg.EntityOutputAlias,
                (ushort)columnCount);
        }

        var rowMatrix = new List<object?[]>();

        while (await reader.ReadAsync(ct))
        {
            var row = new object?[layout.Segments.Length];
            for (int s = 0; s < layout.Segments.Length; s++)
            {
                var seg = layout.Segments[s];
                row[s]  = MaterializerRegistry.Materialize(seg.StorageEntityId, reader, segmentMaps[s]);
            }
            rowMatrix.Add(row);
        }

        return ResultBuilderRegistry.Build<TModel>(rootEntityId, layout, rowMatrix);
    }

    // ---------------------------------------------------------------
    // Entity ID resolution
    // ---------------------------------------------------------------

    private ushort ResolveRootEntityId(string modelName)
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

    private ushort ResolveRootStorageEntityId(string modelName)
    {
        if (!_meta.TryGetEntityId(modelName, out var entityId))
            throw new InvalidOperationException($"Unknown model '{modelName}'.");

        var table = _meta.Table[entityId][0];

        for (ushort i = 0; i < _meta.EntityTable.Length; i++)
        {
            if (string.Equals(
                    _meta.EntityTable[i],
                    table,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new InvalidOperationException(
            $"No storage entity found for model '{modelName}'.");
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