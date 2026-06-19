using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using Dapper;
using FASTER.core;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using Npgsql;

namespace CoffeeBeanery.Service;

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

public class ProcessService<M> : IProcessService<M>
    where M : class
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IFasterKV<string, string> _cache;
    private readonly AdapterLookup _adapterLookup;
    private readonly IEntityMetaProvider _meta;
    private readonly PostgresSqlWriter _sqlWriter;
    private readonly IReadOnlyList<IQueryPlanContributor> _queryContributors;
    private readonly IReadOnlyList<IMutationPlanContributor> _mutationContributors;
    private readonly IPlannerRegistry _plannerRegistry;
    
    public ProcessService(
        NpgsqlDataSource dataSource,
        IFasterKV<string, string> cache,
        AdapterLookup adapterLookup,
        IEntityMetaProvider meta,
        PostgresSqlWriter sqlWriter,
        IPlannerRegistry plannerRegistry,
        IEnumerable<IQueryPlanContributor>? queryContributors = null,
        IEnumerable<IMutationPlanContributor>? mutationContributors = null)
    {
        _dataSource = dataSource;
        _cache = cache;
        _adapterLookup = adapterLookup;
        _meta = meta;
        _sqlWriter = sqlWriter;
        _queryContributors = queryContributors?.ToArray() ?? Array.Empty<IQueryPlanContributor>();
        _mutationContributors = mutationContributors?.ToArray() ?? Array.Empty<IMutationPlanContributor>();
        _plannerRegistry = plannerRegistry;
    }

    // ---------------------------------------------------------------
    // Query
    // ---------------------------------------------------------------

    public async Task<QueryResult<M>> QueryProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId = ResolveRootEntityId(modelName);
        var rootOutputAlias = modelName;

        var selectionSet = selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException("Selection has no SelectionSet.");

        var selectionIr = HotChocolateAdapter.AdaptQuery(
            rootEntityId, rootOutputAlias, selectionSet, _adapterLookup);

        selectionIr = SelectionOptimizer.Optimize(selectionIr);

        var planBuilder = new QueryPlanBuilder();
        planBuilder.SetRoot(rootEntityId, rootOutputAlias);

        _plannerRegistry.Build(rootEntityId, selectionIr, ref planBuilder);

        foreach (var contributor in _queryContributors)
            contributor.Contribute(rootEntityId, selectionIr, ref planBuilder);

        var plan = planBuilder.Build();

        var sql = _sqlWriter.WriteSelect(plan);

        using var connection = await AgeConnectionFactory.OpenAsync(_dataSource);
        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        var models = MaterializeResults<M>(grid, plan);

        return new QueryResult<M>
        {
            Models = models,
            TotalCount = models.Count,
            TotalPageRecords = models.Count
        };
    }

    // ---------------------------------------------------------------
    // Mutation
    // ---------------------------------------------------------------

    public async Task<QueryResult<M>> MutationProcessAsync(
        string cacheKey,
        ISelection selection,
        string modelName,
        CancellationToken cancellationToken)
    {
        var rootEntityId = ResolveRootEntityId(modelName);
        var rootOutputAlias = modelName;

        var mutationArg = selection.SyntaxNode.Arguments
            .FirstOrDefault(a =>
                !string.Equals(a.Name.Value, "where",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "order",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "first",  StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Name.Value, "last",   StringComparison.OrdinalIgnoreCase));

        if (mutationArg?.Value is ObjectValueNode inputObj)
        {
            var mutationIr = HotChocolateAdapter.AdaptMutation(
                rootEntityId, rootOutputAlias, inputObj, _adapterLookup);

            var mutationPlanBuilder = new MutationPlanBuilder();
            _plannerRegistry.BuildMutation(rootEntityId, mutationIr, ref mutationPlanBuilder);

            foreach (var contributor in _mutationContributors)
                contributor.Contribute(rootEntityId, mutationIr, ref mutationPlanBuilder);

            var mutationPlan = mutationPlanBuilder.Build();
            var upsertSql = _sqlWriter.WriteUpserts(mutationPlan);

            if (!string.IsNullOrEmpty(upsertSql))
            {
                using var writeConnection = await AgeConnectionFactory.OpenAsync(_dataSource);
                await writeConnection.ExecuteAsync(
                    new CommandDefinition(upsertSql, cancellationToken: cancellationToken));
            }
        }

        var selectionSet = selection.SyntaxNode.SelectionSet
            ?? throw new InvalidOperationException("Selection has no SelectionSet.");

        var selectionIr = HotChocolateAdapter.AdaptQuery(
            rootEntityId, rootOutputAlias, selectionSet, _adapterLookup);

        selectionIr = SelectionOptimizer.Optimize(selectionIr);

        var queryPlanBuilder = new QueryPlanBuilder();
        queryPlanBuilder.SetRoot(rootEntityId, rootOutputAlias);

        _plannerRegistry.Build(rootEntityId, selectionIr, ref queryPlanBuilder);

        foreach (var contributor in _queryContributors)
            contributor.Contribute(rootEntityId, selectionIr, ref queryPlanBuilder);

        var queryPlan = queryPlanBuilder.Build();
        var selectSql = _sqlWriter.WriteSelect(queryPlan);

        using var readConnection = await AgeConnectionFactory.OpenAsync(_dataSource);
        using var grid = await readConnection.QueryMultipleAsync(
            new CommandDefinition(selectSql, cancellationToken: cancellationToken));

        var models = MaterializeResults<M>(grid, queryPlan);

        return new QueryResult<M>
        {
            Models = models,
            TotalCount = models.Count,
            TotalPageRecords = models.Count
        };
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ushort ResolveRootEntityId(string modelName)
    {
        if (!TryResolveEntityIdByName(modelName, out var id) ||
            !_plannerRegistry.IsValidEntity(id))
        {
            throw new InvalidOperationException(
                $"No planner registered for model '{modelName}'. " +
                $"Is there a mapping class for this model?");
        }

        return id;
    }

    private bool TryResolveEntityIdByName(string modelName, out ushort entityId)
    {
        for (ushort i = 0; i < _meta.Count; i++)
        {
            if (string.Equals(_meta.Table[i], modelName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_plannerRegistry.GetEntityName(i), modelName, StringComparison.OrdinalIgnoreCase))
            {
                entityId = i;
                return true;
            }
        }

        entityId = 0;
        return false;
    }

    private static List<M> MaterializeResults<T>(SqlMapper.GridReader grid, in QueryPlan plan)
        where T : class
    {
        _ = grid.Read<dynamic>().ToList();
        return new List<M>();
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