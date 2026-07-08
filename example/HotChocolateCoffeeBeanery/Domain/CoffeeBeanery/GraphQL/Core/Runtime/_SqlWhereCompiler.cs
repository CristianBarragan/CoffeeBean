// using CoffeeBeanery.GraphQL.Core.Sql;
// using CoffeeBeanery.GraphQL.Helper;
// using HotChocolate.Execution.Processing;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime
// {
//     internal static class SqlWhereCompiler
//     {
//         public static void Compile(
//             SqlCompilationContext context,
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             ISelection selection,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree,
//             string wrapperEntityName,
//             Dictionary<string, string> sqlWhereStatement)
//         {
//             var whereArg = selection.SyntaxNode.Arguments
//                 .FirstOrDefault(a => a.Name.Value.Matches("where"));
//
//             if (whereArg?.Value is ObjectValueNode obj)
//             {
//                 GetFieldsWhere(
//                     entityTrees,
//                     sqlWhereStatement,
//                     obj,
//                     rootTree,
//                     wrapperEntityName,
//                     string.Empty,
//                     Entity.ClauseTypes,
//                     null);
//             }
//
//             context.SqlWhereStatement = sqlWhereStatement;
//         }
//
//         public static void GetFieldsWhere(
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, string> sqlWhereStatement,
//             ObjectValueNode whereNode,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootNode,
//             string wrapperEntityName,
//             string clauseCondition,
//             List<string> clauseType,
//             Dictionary<string, List<string>> permission = null)
//         {
//             GetFieldsWhereRecursive(
//                 entityTrees,
//                 sqlWhereStatement,
//                 whereNode,
//                 rootNode,
//                 wrapperEntityName,
//                 clauseCondition,
//                 clauseType,
//                 permission);
//         }
//
//         private static void GetFieldsWhereRecursive(
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             Dictionary<string, string> sqlWhereStatement,
//             ObjectValueNode whereNode,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentNode,
//             string wrapperEntityName,
//             string clauseCondition,
//             List<string> clauseType,
//             Dictionary<string, List<string>> permission)
//         {
//             foreach (var field in whereNode.Fields)
//             {
//                 var fieldName = field.Name.Value;
//                 var value = field.Value;
//
//                 // ----------------------------
//                 // NAVIGATION OBJECT
//                 // ----------------------------
//                 if (value is ObjectValueNode nestedObj &&
//                     fieldName != "eq" &&
//                     fieldName != "neq" &&
//                     fieldName != "in")
//                 {
//                     var childNode = ResolveChildEntityNode(entityTrees, currentNode, fieldName);
//
//                     if (childNode is null)
//                         continue;
//
//                     GetFieldsWhereRecursive(
//                         entityTrees,
//                         sqlWhereStatement,
//                         nestedObj,
//                         childNode,
//                         wrapperEntityName,
//                         clauseCondition,
//                         clauseType,
//                         permission);
//
//                     continue;
//                 }
//
//                 // ----------------------------
//                 // COLLECTION OPS
//                 // ----------------------------
//                 if (fieldName is "some" or "all" or "any" or "none")
//                 {
//                     if (value is ObjectValueNode inner)
//                     {
//                         GetFieldsWhereRecursive(
//                             entityTrees,
//                             sqlWhereStatement,
//                             inner,
//                             currentNode,
//                             wrapperEntityName,
//                             clauseCondition,
//                             clauseType,
//                             permission);
//                     }
//
//                     continue;
//                 }
//
//                 // ----------------------------
//                 // OPERATOR NODE
//                 // ----------------------------
//                 if (value is not ObjectValueNode opObj)
//                     continue;
//
//                 var fieldMap = currentNode.Mapping
//                     .FirstOrDefault(m => m.SourceName.Matches(fieldName));
//
//                 if (fieldMap is null)
//                     continue;
//
//                 foreach (var opField in opObj.Fields)
//                 {
//                     var op = opField.Name.Value;
//
//                     if (!clauseType.Contains(op))
//                         continue;
//
//                     var clauseValue = opField.Value switch
//                     {
//                         StringValueNode s => s.Value,
//                         IntValueNode i => i.Value.ToString(),
//                         NullValueNode => "null",
//                         _ => opField.Value.ToString()
//                     };
//
//                     var column = fieldMap.DestinationName;
//
//                     string condition = op switch
//                     {
//                         "eq" => clauseValue == "null"
//                             ? $" (~.\"{column}\" IS NULL "
//                             : $" (~.\"{column}\" = '{clauseValue}' ",
//
//                         "neq" => clauseValue == "null"
//                             ? $" (~.\"{column}\" IS NOT NULL "
//                             : $" (~.\"{column}\" <> '{clauseValue}' ",
//
//                         "in" => $" (~.\"{column}\" IN ('{clauseValue}') ",
//
//                         _ => ""
//                     };
//
//                     if (!string.IsNullOrEmpty(condition))
//                     {
//                         AddOrAppend(sqlWhereStatement, currentNode.Alias, condition);
//                     }
//                 }
//             }
//         }
//
//         // ----------------------------
//         // Resolve a nested where-field (e.g. "innerCustomer") to the
//         // CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree it navigates to, via EntityKey.AliasProperty.
//         // Checks both EntityChildren (collection navs) and EntityChildrenRelated
//         // (single-reference navs, e.g. dependent-side FKs like InnerCustomer/OuterCustomer).
//         // ----------------------------
//         private static CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree ResolveChildEntityNode(
//             Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//             CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentNode,
//             string fieldName)
//         {
//             var key = currentNode.EntityChildren
//                 .FirstOrDefault(k => k.AliasProperty.Matches(fieldName));
//
//             key ??= currentNode.EntityChildrenRelated
//                 .FirstOrDefault(k => k.AliasProperty.Matches(fieldName));
//
//             if (key is null)
//                 return null;
//
//             return entityTrees.Values.FirstOrDefault(t =>
//                 t.Alias.Matches(key.AliasTo)) ??
//                 entityTrees.Values.FirstOrDefault(t =>
//                     t.Name.Matches(key.To));
//         }
//
//         private static void AddOrAppend(
//             Dictionary<string, string> sqlWhereStatement,
//             string alias,
//             string condition)
//         {
//             if (sqlWhereStatement.TryGetValue(alias, out var existing))
//             {
//                 if (!existing.Contains(condition))
//                     sqlWhereStatement[alias] += $" AND {condition} ) ";
//             }
//             else
//             {
//                 sqlWhereStatement.Add(alias, $"{condition} ) ");
//             }
//         }
//     }
//
//     public class Entity
//     {
//         public static List<string> ClauseTypes = new()
//         {
//             "eq",
//             "neq",
//             "in",
//             "any"
//         };
//     }
// }