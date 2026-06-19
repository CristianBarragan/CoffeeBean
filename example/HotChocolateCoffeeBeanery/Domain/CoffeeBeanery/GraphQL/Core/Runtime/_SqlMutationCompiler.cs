// using CoffeeBeanery.GraphQL.Core.Sql;
// using HotChocolate.Execution.Processing;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime
// {
//     public static class SqlMutationCompiler
//     {
//         public static void Compile(
//             _SqlCompilationContext context,
//             ISelection rootSelection,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree,
//             Dictionary<string, string> sqlWhereStatement,
//             Dictionary<string, ModelNodeTree> modelTrees,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, ModelNode> modelEntityNodes,
//             Dictionary<string, EntityNode> entityEntityNodes,
//             List<string> models,
//             List<string> entities)
//         {
//             var sqlUpsertStatementNodes =
//                 new Dictionary<string, EntityNode>(StringComparer.OrdinalIgnoreCase);
//
//             var statements = new List<string>();
//             var selectStatements = new List<string>();
//
//             SqlSelectBuilder.GetMutations(
//                 modelTrees,
//                 entityTrees,
//                 rootSelection.SyntaxNode,
//                 modelEntityNodes,
//                 entityEntityNodes,
//                 sqlUpsertStatementNodes,
//                 rootTree,
//                 string.Empty,
//                 models);
//
//             ProcessMutation(
//                 rootTree,
//                 sqlWhereStatement,
//                 modelTrees,
//                 entityTrees,
//                 sqlUpsertStatementNodes,
//                 entities,
//                 statements,
//                 selectStatements);
//
//             context.UpsertSql =
//                 string.Join(";\n",
//                     statements
//                         .Concat(selectStatements)
//                         .Where(x => !string.IsNullOrWhiteSpace(x)));
//         }
//
//         private static void ProcessMutation(
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree,
//             Dictionary<string, string> sqlWhereStatement,
//             Dictionary<string, ModelNodeTree> modelTrees,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, EntityNode> sqlUpsertStatementNodes,
//             List<string> entities,
//             List<string> statements,
//             List<string> selectStatements)
//         {
//             // _SqlHelper.GenerateUpsertStatements(
//             //     modelTrees,
//             //     entityTrees,
//             //     sqlUpsertStatementNodes,
//             //     rootTree,
//             //     entities,
//             //     sqlWhereStatement,
//             //     new List<string>(),
//             //     statements,
//             //     selectStatements);
//         }
//
//         private static ISyntaxNode GetMutationArgument(
//             ISelection rootSelection)
//         {
//             return rootSelection.SyntaxNode
//                 .GetNodes()
//                 .First(x => x.Kind == SyntaxKind.Argument);
//         }
//
//         private static IEnumerable<ISyntaxNode> EnumerateMutations(
//             ISyntaxNode mutationArgument)
//         {
//             if (mutationArgument.ToString().StartsWith("["))
//             {
//                 foreach (var mutationNode in mutationArgument
//                              .GetNodes()
//                              .Last()
//                              .GetNodes())
//                 {
//                     yield return mutationNode;
//                 }
//
//                 yield break;
//             }
//
//             yield return mutationArgument;
//         }
//     }
// }