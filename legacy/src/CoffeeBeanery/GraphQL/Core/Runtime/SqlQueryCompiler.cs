using HotChocolate.Execution.Processing;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;
using FASTER.core;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    internal static class SqlQueryCompiler
    {
        public static void Compile(
            SqlCompilationContext context,
            ISelection rootSelection,
            NodeTree rootTree,
            IFasterKV<string, string> cache,
            string cacheKey)
        {
            var sqlWhereStatement = new Dictionary<string, string>();
            var statementNodes = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
            var visitedModels = new List<string>();
            var visitedEntities = new List<string>();
            var hasSorting = false;
            var hasPagination = false;
            context.Pagination = new Pagination();
            context.EntityTrees = SqlNodeRegistry.EntityTrees;
            var modelSqlNodes = new Dictionary<string, SqlNode>(StringComparer.OrdinalIgnoreCase);
            context.HasTotalCount = rootSelection.SyntaxNode.GetNodes()
                .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList()
                .Any(a => a.ToString().Contains("totalCount"));
            
            context.HasPagination = rootSelection.SyntaxNode.GetNodes()
                .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList()
                .Any(a => a.ToString().Contains("pageInfo"));
            
            SqlSelectBuilder.GetFields(SqlNodeRegistry.ModelTrees, SqlNodeRegistry.EntityTrees, rootSelection.SyntaxNode.GetNodes()
                    .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().ToList().FirstOrDefault(a => a.ToString().Contains("edges")), 
                SqlNodeRegistry.EntityNodes, SqlNodeRegistry.ModelNodes, statementNodes, rootTree, visitedModels, 
                visitedEntities, SqlNodeRegistry.ModelNames, modelSqlNodes, true);

            SqlSelectBuilder.GetFields(SqlNodeRegistry.ModelTrees, SqlNodeRegistry.EntityTrees, rootSelection.SyntaxNode.GetNodes()
                    .ToList().Last(a => a.Kind == SyntaxKind.SelectionSet).GetNodes().FirstOrDefault(a => a.ToString().StartsWith("nodes")), SqlNodeRegistry.EntityNodes,
                SqlNodeRegistry.ModelNodes, statementNodes, rootTree, visitedModels, visitedEntities,
                SqlNodeRegistry.ModelNames, modelSqlNodes, false);
            
            SqlWhereCompiler.Compile(context, modelSqlNodes, statementNodes, rootSelection, rootTree, 
                rootTree.Name, sqlWhereStatement);
            
            SqlSelectBuilder.HandleGraphQL(context, SqlNodeRegistry.EntityNodes, statementNodes, 
                SqlNodeRegistry.EntityTrees, rootTree, cache, cacheKey);

            foreach (var argument in rootSelection.SyntaxNode.Arguments
                         .Where(a => !a.Name.Value.Matches("where")))
            {
                switch (argument.Name.ToString())
                {
                    case "first":
                        context.Pagination.First = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                            ? 0
                            : int.Parse(argument.Value?.Value.ToString());
                        hasPagination = true;
                        break;
                    case "last":
                        context.Pagination.Last = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                            ? 0
                            : int.Parse(argument.Value?.Value.ToString());
                        hasPagination = true;
                        break;
                    case "before":
                        context.Pagination.Before = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                            ? ""
                            : argument.Value?.Value.ToString();
                        hasPagination = true;
                        break;
                    case "after":
                        context.Pagination.After = string.IsNullOrEmpty(argument.Value?.Value?.ToString())
                            ? ""
                            : argument.Value?.Value.ToString();
                        hasPagination = true;
                        break;
                }
                
                if (argument.Name.ToString().Contains("order"))
                {
                    hasSorting = true;
                    SqlOrderCompiler.Compile(context, SqlNodeRegistry.ModelTrees, argument,
                        rootTree, SqlNodeRegistry.ModelNodes, SqlNodeRegistry.EntityTrees);
                }
            }
            
            if (hasPagination || hasSorting)
            {
                context.HasPagination = true;
                SqlPagingCompiler.GetPagination(rootTree, context, rootSelection);
            }
            
            context.SqlNodesApplied = statementNodes;
        }
    }

}