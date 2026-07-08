using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoffeeBeanery.Service;

public class ProcessQuery<M> : IQuery<ProcessQueryParameters,
    (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
    where M : class
{
    private readonly ILogger<ProcessQuery<M>> _logger;
    private readonly NpgsqlDataSource _db;

    public ProcessQuery(ILoggerFactory loggerFactory, NpgsqlDataSource db)
    {
        _logger = loggerFactory.CreateLogger<ProcessQuery<M>>();
        _db = db;
    }

    public async Task<(List<M>, int?, int?, int?, int?)> ExecuteAsync(
        ProcessQueryParameters parameters,
        CancellationToken ct)
    {
        var context = parameters.Context;

        var query = string.IsNullOrEmpty(context.UpsertSql)
            ? context.SelectSql
            : context.UpsertSql + ";" + context.SelectSql;

        await using var connection = await AgeConnectionFactory.OpenAsync(_db);
        await using var tx = await connection.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.Transaction = (NpgsqlTransaction)tx;

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (reader.FieldCount == 0)
            {
                if (!await reader.NextResultAsync(ct))
                    throw new InvalidOperationException(
                        "Expected a SELECT result set but none was found.");
            }

            var layout = RowLayout.FromQueryPlan(context.QueryPlan);
            var rowMatrix = new List<object?[]>();

            while (await reader.ReadAsync(ct))
            {
                var row = new object?[layout.Segments.Length];
                for (int s = 0; s < layout.Segments.Length; s++)
                {
                    var seg = layout.Segments[s];
                    // row[s] = MaterializerRegistry.Materialize(seg.StorageEntityId, reader, seg.OrdinalStart);
                }
                rowMatrix.Add(row);
            }

            await tx.CommitAsync(ct);

            var result = MappingConfiguration(context, layout, rowMatrix);

            return (
                result.models,
                result.startCursor ?? context.Pagination?.StartCursor,
                result.endCursor   ?? context.Pagination?.EndCursor,
                result.totalCount,
                result.totalPageRecords);
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) { }
            catch (Exception rollbackEx) { _logger.LogWarning(rollbackEx, "Rollback attempt failed"); }

            _logger.LogError(ex, "ProcessQuery failed");
            return ([], 0, 0, 0, 0);
        }
    }

    public virtual (List<M> models, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)
        MappingConfiguration(SqlCompilationContext context, RowLayout layout, List<object?[]> rowMatrix)
    {
        throw new NotImplementedException();
    }
}