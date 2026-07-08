// using HotChocolate.Execution.Processing;
// using CoffeeBeanery.GraphQL.Core.GraphQL;
// using CoffeeBeanery.GraphQL.Core.Sql;
// using CoffeeBeanery.GraphQL.Helper;
// using FASTER.core;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime
// {
//     internal static class SqlQueryCompiler
//     {
//         public static void Compile(
//             SqlCompilationContext context,
//             ISelection rootSelection,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree,
//             IFasterKV<string, string> cache,
//             string cacheKey)
//         {
//             var sqlWhereStatement = new Dictionary<string, string>();
//             var statementNodes = new Dictionary<string, EntityNode>(StringComparer.OrdinalIgnoreCase);
//             var visitedModels = new List<string>();
//             var visitedEntities = new List<string>();
//             var hasSorting = false;
//             var hasPagination = false;
//             context.Pagination = new Pagination();
//             context.EntityTrees = NodeRegistry.EntityTrees;
//             var modelEntityNodes = new Dictionary<string, EntityNode>(StringComparer.OrdinalIgnoreCase);
//             context.HasTotalCount = rootSelection.SyntaxNode.GetNodes()
//                 .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList()
//                 .Any(a => a.ToString().Contains("totalCount"));
//             
//             context.HasPagination = rootSelection.SyntaxNode.GetNodes()
//                 .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList()
//                 .Any(a => a.ToString().Contains("pageInfo"));
//
//             var modelNames = NodeRegistry.ModelTrees.Select(a => a.Value.Name).ToList();
//             
//             SqlSelectBuilder.GetFields(NodeRegistry.ModelTrees, NodeRegistry.EntityTrees, rootSelection.SyntaxNode.GetNodes()
//                     .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList().FirstOrDefault(a => a.ToString().Contains("edges")), 
//                 NodeRegistry.EntityNodes, NodeRegistry.ModelNodes, statementNodes, rootTree, visitedModels, 
//                 visitedEntities, modelNames, modelEntityNodes, true);
//
//             SqlSelectBuilder.GetFields(NodeRegistry.ModelTrees, NodeRegistry.EntityTrees, rootSelection.SyntaxNode.GetNodes()
//                     .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().FirstOrDefault(a => a.ToString().StartsWith("nodes")), NodeRegistry.EntityNodes,
//                 NodeRegistry.ModelNodes, statementNodes, rootTree, visitedModels, visitedEntities,
//                 modelNames, modelEntityNodes, false);
//             
//             // SqlWhereCompiler.Compile(context, modelEntityNodes, statementNodes, rootSelection, rootTree, 
//             //     rootTree.Name, sqlWhereStatement);
//             
//             SqlSelectBuilder.HandleGraphQL(context, NodeRegistry.EntityNodes, statementNodes, 
//                 NodeRegistry.EntityTrees, rootTree, cache, cacheKey);
//
//             foreach (var argument in rootSelection.SyntaxNode.Arguments
//                          .Where(a => !a.Name.Value.Matches("where")))
//             {
//                 switch (argument.Name.ToString())
//                 {
//                     case "first":
//                         context.Pagination.First = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? 0
//                             : int.Parse(argument.Value?.Value.ToString());
//                         hasPagination = true;
//                         break;
//                     case "last":
//                         context.Pagination.Last = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? 0
//                             : int.Parse(argument.Value?.Value.ToString());
//                         hasPagination = true;
//                         break;
//                     case "before":
//                         context.Pagination.Before = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? ""
//                             : argument.Value?.Value.ToString();
//                         hasPagination = true;
//                         break;
//                     case "after":
//                         context.Pagination.After = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
//                             ? ""
//                             : argument.Value?.Value.ToString();
//                         hasPagination = true;
//                         break;
//                 }
//                 
//                 if (argument.Name.ToString().Contains("order"))
//                 {
//                     hasSorting = true;
//                     SqlOrderCompiler.Compile(context, NodeRegistry.ModelTrees, argument,
//                         rootTree, NodeRegistry.ModelNodes, NodeRegistry.EntityTrees);
//                 }
//             }
//             
//             if (hasPagination || hasSorting)
//             {
//                 context.HasPagination = true;
//                 SqlPagingCompiler.GetPagination(rootTree, context, rootSelection);
//             }
//             
//             context.EntityNodesApplied = statementNodes;
//         }
//     }
//
// }