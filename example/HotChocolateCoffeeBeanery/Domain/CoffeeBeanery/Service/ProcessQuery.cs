// using CoffeeBeanery.CQRS;
// using CoffeeBeanery.GraphQL.Core.Runtime;
// using CoffeeBeanery.GraphQL.Core.Sql;
// using Dapper;
// using Npgsql;
//
// namespace CoffeeBeanery.Service;
//
// public class ProcessQuery<M> : IQuery<ProcessQueryParameters,
//     (List<M> list, int? startCursor, int? endCursor, int? totalCount, int? totalPageRecords)>
//     where M : class
// {
//     private readonly ILogger<ProcessQuery<M>> _logger;
//     private readonly NpgsqlDataSource _db;
//
//     public ProcessQuery(
//         ILoggerFactory loggerFactory,
//         NpgsqlDataSource db)
//     {
//         _logger = loggerFactory.CreateLogger<ProcessQuery<M>>();
//         _db = db;
//     }
//
//     public async Task<(List<M>, int?, int?, int?, int?)> ExecuteAsync(
//         ProcessQueryParameters parameters,
//         CancellationToken ct)
//     {
//         var context = parameters.Context;
//         context.SplitOnDapper = context.SplitOnDapper
//             .OrderBy(a => a.Key.Split('~')[1].Length)
//             .ToDictionary(a => a.Key, a => a.Value);
//
//         // Key format is "{alias}~{splitOnColumn}". Keep the alias alongside the column/type
//         // so the materializer can map each row-array slot back to the mapping it came from.
//         var orderedEntries = context.SplitOnDapper
//             .Select(a => (Alias: a.Key.Split('~')[0], SplitOnColumn: a.Key.Split('~')[1], Type: a.Value))
//             .ToList();
//
//         var aliasOrder = orderedEntries.Select(e => e.Alias).ToList();
//         var types      = orderedEntries.Select(e => e.Type).ToList();
//         var splitOn    = orderedEntries.Select(e => e.SplitOnColumn).ToList();
//
//         var query = context.UpsertSql + ";" + context.SelectSql;
//
//         await using var connection = await AgeConnectionFactory.OpenAsync(_db);
//         await using var tx = await connection.BeginTransactionAsync(ct);
//
//         try
//         {
//             var rowMatrix = new List<object?[]>();
//
//             await connection.QueryAsync(
//                 query,
//                 types.ToArray(),
//                 row =>
//                 {
//                     rowMatrix.Add((object?[])row.Clone());
//                     return 0;
//                 },
//                 splitOn:     string.Join(",", splitOn),
//                 transaction: tx);
//
//             await tx.CommitAsync(ct);
//
//             var result = MappingConfiguration(context, aliasOrder, rowMatrix, types);
//
//             return (
//                 result.models,
//                 result.startCursor ?? context.Pagination?.StartCursor,
//                 result.endCursor   ?? context.Pagination?.EndCursor,
//                 result.totalCount,
//                 result.totalPageRecords);
//         }
//         catch (Exception ex)
//         {
//             try
//             {
//                 await tx.RollbackAsync(CancellationToken.None);
//             }
//             catch (InvalidOperationException)
//             {
//             }
//             catch (Exception rollbackEx)
//             {
//                 _logger.LogWarning(rollbackEx, "Rollback attempt failed");
//             }
//
//             _logger.LogError(ex, "ProcessQuery failed");
//             return ([], 0, 0, 0, 0);
//         }
//     }
//
//     public virtual (List<M> models,
//                     int? startCursor,
//                     int? endCursor,
//                     int? totalCount,
//                     int? totalPageRecords)
//         MappingConfiguration(
//             _SqlCompilationContext context,
//             List<string> aliasOrder,
//             List<object?[]> rowMatrix,
//             List<Type> types)
//     {
//         // var models = DynamicGraphMaterializer.Materialize<M>(aliasOrder, rowMatrix);
//         //
//         // // Cursor/paging numbers are whatever your pagination scheme keys off of - plug in
//         // // the real property names once Pagination's shape is settled. Left as a hook so
//         // // ExecuteAsync's `?? context.Pagination?.StartCursor` fallback still applies.
//         // return (models, null, null, models.Count, models.Count);
//         throw new NotImplementedException();
//     }
// }