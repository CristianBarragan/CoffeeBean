// using System.Text;
// using CoffeeBeanery.GraphQL.Helper;
// using HotChocolate.Execution.Processing;
// using Microsoft.EntityFrameworkCore.Query;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime
// {
//     internal static class SqlPagingCompiler
//     {
//         public static void GetPagination(CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree, SqlCompilationContext ctx, ISelection rootSelection)
//         {
//             var hasPagination = false;
//             
//             foreach (var argument in rootSelection.SyntaxNode.Arguments.Where(a => !a.Name.Value.Matches("where")))
//             {
//                 switch (argument.Name.ToString())
//                 {
//                     case "first":
//                         ctx.Pagination.First = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? 0
//                             : int.Parse(argument.Value?.Value.ToString());
//                         hasPagination = true;
//                         break;
//                     case "last":
//                         ctx.Pagination.Last = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? 0
//                             : int.Parse(argument.Value?.Value.ToString());
//                         hasPagination = true;
//                         break;
//                     case "before":
//                         ctx.Pagination.Before = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? ""
//                             : argument.Value?.Value.ToString();
//                         hasPagination = true;
//                         break;
//                     case "after":
//                         ctx.Pagination.After = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? ""
//                             : argument.Value?.Value.ToString();
//                         hasPagination = true;
//                         break;
//                 }
//             }
//
//             if (hasPagination)
//             {
//                 HandleQueryClause(rootTree, ctx);    
//             }
//         }
//         
//         private static void HandleQueryClause(CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree, SqlCompilationContext ctx)
//         {
//             var sqlQuery = new StringBuilder();
//             sqlQuery.AppendLine(ctx.SelectSql);
//             var from = 1;
//             var to = ctx.Pagination!.PageSize;
//             var sqlWhereStatement = string.Empty;
//
//             if (!string.IsNullOrEmpty(ctx.Pagination!.After) && ctx.Pagination.First > 0 &&
//                 ctx.HasTotalCount)
//             {
//                 from = int.Parse(ctx.Pagination.After) + 1;
//                 to = from + ctx.Pagination.First!.Value;
//                 sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
//                     ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
//                     : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
//             }
//             else if (!string.IsNullOrEmpty(ctx.Pagination?.Before) && ctx.Pagination.Last > 0 &&
//                 ctx.HasTotalCount)
//             {
//                 to = int.Parse(ctx.Pagination.Before) - 1;
//                 from = to - ctx.Pagination.Last!.Value;
//                 to = to >= 1 ? to : 1;
//                 from = from >= 1 ? from : 1;
//                 sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
//                     ? $" WHERE \"RowNumber\" BETWEEN {from} AND {to}"
//                     : $" AND \"RowNumber\" BETWEEN {from} AND {to}";
//             }
//             else if (ctx.Pagination!.First > 0 && ctx.Pagination!.Last > 0 && ctx.HasTotalCount)
//             {
//                 to = ctx.Pagination!.First!.Value;
//                 sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
//                     ? $" WHERE \"RowNumber\" BETWEEN {ctx.Pagination!.First} AND {ctx.Pagination!.Last}"
//                     : $" AND \"RowNumber\" BETWEEN {ctx.Pagination!.First} AND {ctx.Pagination!.Last}";
//             }
//             else if (ctx.Pagination!.First > 0 && ctx.HasTotalCount)
//             {
//                 to = ctx.Pagination!.First!.Value;
//                 sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
//                     ? $" WHERE \"RowNumber\" BETWEEN {ctx.Pagination!.First} AND \"RowNumber\""
//                     : $" AND \"RowNumber\" BETWEEN {ctx.Pagination!.First} AND \"RowNumber\"";
//             }
//             else if (ctx.Pagination!.Last > 0 && ctx.HasTotalCount)
//             {
//                 sqlWhereStatement += string.IsNullOrEmpty(sqlWhereStatement)
//                     ? $" WHERE \"RowNumber\" BETWEEN \"RowNumber\" - {ctx.Pagination!.Last} AND \"RowNumber\""
//                     : $" AND \"RowNumber\" BETWEEN \"RowNumber\" - {ctx.Pagination!.Last} AND \"RowNumber\"";
//             }
//             else
//             {
//                 to = 0;
//                 from = 0;
//             }
//
//             var hasPagination = ctx.Pagination.First > 0 || ctx.Pagination.Last > 0 ||
//                                 (ctx.Pagination.First > 0 &&
//                                  !string.IsNullOrEmpty(ctx.Pagination.After)) ||
//                                 (ctx.Pagination.Last > 0 &&
//                                  !string.IsNullOrEmpty(ctx.Pagination.Before));
//             var sql = $"WITH {rootTree.Schema}s AS (SELECT * FROM (SELECT * FROM (" + sqlQuery + $") {rootTree.Name} ) ";
//             
//             var orderBy = ctx.SqlOrderStatements.Count == 0
//                 ? $"\"{"Id".ToString().ToSnakeCase(ctx.EntityTrees[rootTree.ModelToEntity[0].AliasTo].Id)}\""
//                 : string.Join(",",  ctx.SqlOrderStatements.Select(a => a.Value)).Replace("~*~", $"{rootTree.Schema}s");
//             
//             var totalCount = hasPagination && ctx.HasTotalCount
//                 ? $" DENSE_RANK() OVER( ORDER BY {orderBy}) AS \"RowNumber\","
//                 : "";
//             sqlQuery.Clear();
//             sqlQuery.Append($" {sql} a ) " +
//                             $"SELECT * FROM ( SELECT (SELECT DISTINCT COUNT(1) FROM {rootTree.Schema}s) \"RecordCount\", " +
//                             $"{totalCount} * FROM {rootTree.Schema}s) a {sqlWhereStatement.Replace('~', 'a')}");
//             
//             ctx.SelectSql = sqlQuery.ToString();
//         }
//     }
// }