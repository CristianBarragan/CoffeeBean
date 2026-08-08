using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using Dapper;
using Npgsql;

namespace CoffeeBeanery.Service;

public class ProcessQuery<M> : IQuery<ProcessQueryParameters,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class
{
    private readonly ILogger<ProcessQuery<M>> _logger;
    private readonly NpgsqlDataSource  _db;

    public ProcessQuery(
        ILoggerFactory loggerFactory,
        NpgsqlDataSource  db)
    {
        _logger = loggerFactory.CreateLogger<ProcessQuery<M>>();
        _db = db;
    }

    public async Task<(List<M>, int?, int?, int?, int?)> ExecuteAsync(
        ProcessQueryParameters parameters,
        CancellationToken ct)
    {
        var context = parameters.Context;
        context.SplitOnDapper = context.SplitOnDapper.OrderBy(a => a.Key.Split('~')[1].Length).ToDictionary(a => a.Key, a => a.Value);
        var orderedSplitOn = context.SplitOnDapper.ToDictionary(a => a.Key.Split('~')[1], a => a.Value); 
        var types   = orderedSplitOn.Values.ToList();
        var splitOn = orderedSplitOn.Keys.ToList();

        var query = context.UpsertSql + ";" +
                    context.SelectSql;
        
        await using var connection = await AgeConnectionFactory.OpenAsync(_db);

        await using var tx = await connection.BeginTransactionAsync(ct);
        
        try
        {
            var rowMatrix = new List<object[]>();

            await connection.QueryAsync(
                query,
                types.ToArray(),
                (object[] row) =>
                {
                    rowMatrix.Add((object[])row.Clone());
                    return 0;
                },
                splitOn:     string.Join(",", splitOn),
                transaction: tx);

            await tx.CommitAsync(ct);

            var result = MappingConfiguration(
                context,
                rowMatrix,
                types
                );

            return (
                result.models,
                result.startCursor  ?? context.Pagination?.StartCursor,
                result.endCursor    ?? context.Pagination?.EndCursor,
                result.totalCount,
                result.totalPageRecords);
        }
        catch (Exception ex)
        {
            if (tx is not null)
            {
                try
                {
                    await tx.RollbackAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogWarning(rollbackEx, "Rollback attempt failed");
                }
            }

            _logger.LogError(ex, "ProcessQuery failed");
            return ([], 0, 0, 0, 0);
        }
    }

    public virtual (List<M> models,
                    int? startCursor,
                    int? endCursor,
                    int? totalCount,
                    int? totalPageRecords)
        MappingConfiguration(
            SqlCompilationContext context,
            List<object[]>                  rowMatrix,
            List<Type>                      types)
    {
        throw new NotImplementedException();
    }
}