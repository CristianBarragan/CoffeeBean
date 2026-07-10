// using System.Text;
// using CoffeeBeanery.GraphQL.Core.Sql;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime
// {
//     public static class _SqlSelectStatementBuilder
//     {
//         public sealed class Result
//         {
//             public string Sql { get; init; } = string.Empty;
//             public List<int> NodeIdOrder { get; init; } = new();
//             public List<string> AliasOrder { get; init; } = new();
//             public List<string> SqlAliasOrder { get; init; } = new();
//             public List<Type> Types { get; init; } = new();
//             public List<string> PkColumnOrder { get; init; } = new();
//             public string SplitOn => string.Join(",", PkColumnOrder.Skip(1));
//         }
//
//         public static Result Build(
//             Dictionary<string, EntityNodeTree> entityTrees,
//             Dictionary<string, ModelNodeTree> modelTrees,
//             _ExecutionPlan plan,
//             EntityNodeTree rootTree,
//             Dictionary<string, string> sqlWhereStatement,
//             Dictionary<string, string> sqlOrderStatement)
//         {
//             var selectCols = new List<string>();
//             var joins = new StringBuilder();
//
//             var orderedNodeIds = new List<int>();
//             var orderedAliases = new List<string>();
//             var orderedSqlAliases = new List<string>();
//             var orderedTypes = new List<Type>();
//             var orderedPkColumns = new List<string>();
//
//             var sqlAliasByNodeId = new Dictionary<int, string>();
//
//             var aliasUseCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
//             string BuildSqlAlias(string alias)
//             {
//                 var count = aliasUseCount.TryGetValue(alias, out var c) ? c + 1 : 1;
//                 aliasUseCount[alias] = count;
//                 return count == 1 ? alias : $"{alias}_{count}";
//             }
//
//             void EmitColumnsFor(ExecutionNode node, EntityNodeTree tree, string sqlAlias)
//             {
//                 orderedNodeIds.Add(node.Id);
//                 orderedAliases.Add(tree.Alias);
//                 orderedSqlAliases.Add(sqlAlias);
//                 orderedTypes.Add(tree.EntityType);
//
//                 if (node.Columns.Count == 0)
//                 {
//                     selectCols.Add($"\"{sqlAlias}\".\"Id\" AS \"{sqlAlias}_Id\"");
//                     orderedPkColumns.Add($"{sqlAlias}_Id");
//                     return;
//                 }
//
//                 var pkColumn = node.Columns[0];
//                 var pkOutName = sqlAlias == tree.Alias ? pkColumn : $"{sqlAlias}_{pkColumn}";
//                 selectCols.Add($"\"{sqlAlias}\".\"{pkColumn}\" AS \"{pkOutName}\"");
//                 orderedPkColumns.Add(pkOutName);
//
//                 foreach (var col in node.Columns.Skip(1))
//                 {
//                     var outName = sqlAlias == tree.Alias ? col : $"{sqlAlias}_{col}";
//                     selectCols.Add($"\"{sqlAlias}\".\"{col}\" AS \"{outName}\"");
//                 }
//             }
//
//             _ExecutionEngine.Traverse(plan, (node, edge) =>
//             {
//                 EntityNodeTree tree;
//
//                 if (modelTrees.TryGetValue(node.Alias, out var modelTree))
//                 {
//                     if (!entityTrees.TryGetValue(modelTree.EntityType!.Name, out tree))
//                         return;
//                 }
//                 else if (!entityTrees.TryGetValue(node.Alias, out tree))
//                 {
//                     return;
//                 }
//
//                 string sqlAlias;
//
//                 if (edge is null)
//                 {
//                     sqlAlias = BuildSqlAlias(node.Alias);
//                 }
//                 else
//                 {
//                     if (!sqlAliasByNodeId.TryGetValue(edge.From, out var parentSqlAlias))
//                         return;
//
//                     if (edge.Path != null && edge.Path.Count > 1)
//                     {
//                         var prevAlias = parentSqlAlias;
//                         var prevColumn = edge.FromColumn;
//
//                         for (var i = 0; i < edge.Path.Count - 1; i++)
//                         {
//                             var hop = edge.Path[i];
//                             var hopAlias = BuildSqlAlias(hop.TableName);
//
//                             joins.AppendLine(
//                                 $"LEFT JOIN \"{hop.Schema}\".\"{hop.TableName}\" \"{hopAlias}\" " +
//                                 $"ON \"{hopAlias}\".\"{hop.FromColumn}\" = \"{prevAlias}\".\"{prevColumn}\"");
//
//                             prevAlias = hopAlias;
//                             prevColumn = hop.ToColumn;
//                         }
//
//                         sqlAlias = BuildSqlAlias(node.Alias);
//
//                         var lastHop = edge.Path[^1];
//
//                         joins.AppendLine(
//                             $"LEFT JOIN \"{tree.Schema}\".\"{tree.Name}\" \"{sqlAlias}\" " +
//                             $"ON \"{sqlAlias}\".\"{lastHop.ToColumn}\" = \"{prevAlias}\".\"{prevColumn}\"");
//                     }
//                     else
//                     {
//                         sqlAlias = BuildSqlAlias(node.Alias);
//
//                         joins.AppendLine(
//                             $"LEFT JOIN \"{tree.Schema}\".\"{tree.Name}\" \"{sqlAlias}\" " +
//                             $"ON \"{sqlAlias}\".\"{edge.ToColumn}\" = \"{parentSqlAlias}\".\"{edge.FromColumn}\"");
//                     }
//                 }
//
//                 sqlAliasByNodeId[node.Id] = sqlAlias;
//                 EmitColumnsFor(node, tree, sqlAlias);
//             });
//
//             var rootSqlAlias = sqlAliasByNodeId[plan.RootNodeId];
//
//             var sb = new StringBuilder();
//             sb.Append("SELECT DISTINCT ").AppendLine(string.Join(", ", selectCols));
//             sb.AppendLine($"FROM \"{rootTree.Schema}\".\"{rootTree.Name}\" \"{rootSqlAlias}\"");
//             sb.Append(joins);
//
//             var whereParts = sqlWhereStatement
//                 .Where(kv => orderedAliases.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
//                 .Select(kv =>
//                 {
//                     var nodeId = orderedNodeIds.FirstOrDefault(id =>
//                         string.Equals(plan.Nodes[id].Alias, kv.Key, StringComparison.OrdinalIgnoreCase));
//                     var sqlAlias = sqlAliasByNodeId.TryGetValue(nodeId, out var a) ? a : kv.Key;
//                     return kv.Value.Replace("~", sqlAlias);
//                 })
//                 .ToList();
//
//             if (whereParts.Count > 0)
//                 sb.AppendLine("WHERE " + string.Join(" AND ", whereParts));
//
//             if (sqlOrderStatement.Count > 0)
//             {
//                 var orderSql = string.Join(", ", sqlOrderStatement.Select(kv => kv.Value.Replace("~*~", $"\"{kv.Key}\"")));
//                 sb.AppendLine("ORDER BY " + orderSql);
//             }
//
//             return new Result
//             {
//                 Sql = sb.ToString(),
//                 NodeIdOrder = orderedNodeIds,
//                 AliasOrder = orderedAliases,
//                 SqlAliasOrder = orderedSqlAliases,
//                 Types = orderedTypes,
//                 PkColumnOrder = orderedPkColumns
//             };
//         }
//     }
// }