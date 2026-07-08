// using System.Text;
// using CoffeeBeanery.GraphQL.Core.Sql;
// using CoffeeBeanery.GraphQL.Helper;
// using FASTER.core;
// using HotChocolate.Language;
//
// namespace CoffeeBeanery.GraphQL.Core.Runtime;
//
// public class SqlSelectBuilder
// {
//     public static void HandleGraphQL(
//         SqlCompilationContext context,
//         Dictionary<string, EntityNode> EntityNodes,
//         Dictionary<string, EntityNode> EntityNodeStatements,
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree rootTree,
//         IFasterKV<string, string> cache,
//         string cacheKey,
//         Dictionary<string, List<string>> permissions = null)
//     {
//         var sqlQueryStatement = new StringBuilder();
//         var sqlQueryStructures = new Dictionary<string, SqlQueryStructure>(StringComparer.OrdinalIgnoreCase);
//         var splitOnDapper = new Dictionary<string, Type>();
//         var removeOnDapper = new Dictionary<string, Type>();
//         var entityOrder = new List<string>();
//
//         var entityTypes = entityTrees.Select(a => a.Value.EntityType).ToList();
//
//         var rootEntity = entityTrees.Values.FirstOrDefault(a =>
//             rootTree.ModelToEntity.FirstOrDefault() != null &&
//             a.Alias.Matches(rootTree.ModelToEntity.First().AliasTo));
//
//         if (rootEntity == null)
//             throw new InvalidOperationException($"Root entity not found for alias {rootTree.Alias}");
//
//         var splitKey = $"{rootEntity.Alias}~Id";
//
//         splitOnDapper.TryAdd(splitKey,
//             entityTypes.FirstOrDefault(e => e.Name.Matches(rootEntity.Name)));
//
//         GenerateQuery(
//             entityTrees,
//             entityTypes,
//             EntityNodes,
//             sqlQueryStatement,
//             EntityNodeStatements,
//             context.SqlWhereStatement,
//             context.SqlOrderStatements,
//             rootEntity,
//             sqlQueryStructures,
//             splitOnDapper,
//             removeOnDapper,
//             entityOrder,
//             new List<string>());
//
//         var queryStructure = sqlQueryStructures.FirstOrDefault();
//         context.SelectSql = queryStructure.Value.Query;
//         context.SplitOnDapper = splitOnDapper;
//     }
//
//     // ---------------------------------------------------------------------
//     // MUTATIONS
//     // ---------------------------------------------------------------------
//
//     public static void GetMutations(
//         Dictionary<string, ModelNodeTree> trees,
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         ISyntaxNode node,
//         Dictionary<string, ModelNode> linkModelDictionaryTree,
//         Dictionary<string, EntityNode> linkEntityDictionaryTree,
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree,
//         string previousNode,
//         List<string> models)
//     {
//         if (node == null) return;
//
//         var nodeName = node.ToString();
//
//         var colonIndex = nodeName.IndexOf(':');
//         if (colonIndex > 0)
//         {
//             var fieldName = nodeName[..colonIndex].Trim();
//             var rawValue  = nodeName[(colonIndex + 1)..].Trim().Sanitize().Replace("_", "");
//
//             var lookupKey = $"{currentTree.Alias}~{currentTree.Name}~{fieldName}";
//
//             if (linkModelDictionaryTree.TryGetValue(lookupKey, out var fromNode) &&
//                 linkEntityDictionaryTree.TryGetValue(fromNode.RelationshipKey, out var toNode))
//             {
//                 var enumValue = toNode.FromEnumeration
//                     .FirstOrDefault(a => a.Key.Matches(rawValue));
//
//                 toNode.Value = !string.IsNullOrEmpty(enumValue.Key)
//                     ? enumValue.Value.ToString()
//                     : rawValue;
//
//                 AddEntity(linkEntityDictionaryTree, sqlStatementNodes,
//                     entityTrees, currentTree, toNode, !string.IsNullOrEmpty(enumValue.Key));
//
//                 return;
//             }
//         }
//
//         foreach (var childNode in node.GetNodes())
//         {
//             var nameNode = childNode.Kind == SyntaxKind.Name
//                 ? childNode
//                 : childNode.GetNodes().FirstOrDefault(n => n.Kind == SyntaxKind.Name);
//
//             var fieldName = nameNode?.ToString();
//             var childTree = currentTree;
//
//             if (!string.IsNullOrEmpty(fieldName))
//             {
//                 var matched = trees.Values.FirstOrDefault(t =>
//                     fieldName.Matches(t.ModelName) ||
//                     fieldName.Matches(t.Alias));
//
//                 if (matched != null)
//                     childTree = entityTrees[matched.Alias];
//             }
//
//             GetMutations(trees, entityTrees, childNode,
//                 linkModelDictionaryTree,
//                 linkEntityDictionaryTree,
//                 sqlStatementNodes,
//                 childTree,
//                 childNode.ToString(),
//                 models);
//         }
//     }
//
//     // ---------------------------------------------------------------------
//     // FIXED AddEntity (SAFE + NO NULL SPLITS)
//     // ---------------------------------------------------------------------
//
//     private static void AddEntity(
//         Dictionary<string, EntityNode> linkEntityDictionaryTree,
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree,
//         EntityNode EntityNode,
//         bool isEnum)
//     {
//         if (EntityNode == null) return;
//
//         var parts = EntityNode.RelationshipKey?.Split('~');
//         if (parts == null || parts.Length < 3) return;
//
//         foreach (var auxFieldMap in currentTree.NodeMap?.FieldMaps
//                      .Where(f => f.SourceName.Matches(parts[2])) ?? [])
//         {
//             var entity = EntityNode.Clone() as EntityNode;
//             if (entity == null) continue;
//
//             entity.Value = string.IsNullOrEmpty(entity.Value)
//                 ? EntityNode.Value
//                 : entity.Value;
//
//             entity.Alias = auxFieldMap.DestinationAlias;
//             entity.Table = currentTree.IsEntity
//                 ? currentTree.Name
//                 : auxFieldMap.DestinationEntity;
//
//             entity.RelationshipKey =
//                 $"{(currentTree.IsEntity ? currentTree.Alias : auxFieldMap.DestinationAlias)}~" +
//                 $"{(currentTree.IsEntity ? currentTree.Name : auxFieldMap.DestinationEntity)}~" +
//                 $"{parts[2]}";
//
//             entity.FromComplexModel = !currentTree.IsEntity;
//
//             sqlStatementNodes.TryAdd(entity.RelationshipKey, entity);
//         }
//     }
//
//     // ---------------------------------------------------------------------
//     // FIXED GetFields (uses EntityChildren + EntityChildrenRelated)
//     // ---------------------------------------------------------------------
//
//     public static void GetFields(
//         Dictionary<string, ModelNodeTree> trees,
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         ISyntaxNode node,
//         Dictionary<string, EntityNode> linkEntityDictionaryTree,
//         Dictionary<string, ModelNode> linkModelDictionaryTree,
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree,
//         List<string> visitedModels,
//         List<string> visitedEntities,
//         List<string> models,
//         Dictionary<string, EntityNode> modelEntityNodes,
//         bool isEdge)
//     {
//         if (node != null && node.GetNodes()?.Count() == 0)
//         {
//             var fieldName = node.ToString().Trim();
//
//             if (linkModelDictionaryTree.TryGetValue(
//                     $"{currentTree.Alias}~{currentTree.Name}~{fieldName}",
//                     out var ModelNodeFrom))
//             {
//                 modelEntityNodes[ModelNodeFrom.RelationshipKey] = linkEntityDictionaryTree[ModelNodeFrom.RelationshipKey];
//
//                 var fieldMap = currentTree.NodeMap?.FieldMaps
//                     .FirstOrDefault(f => f.SourceName.Matches(fieldName));
//
//                 var destinationEntity = fieldMap?.DestinationEntity;
//
//                 if (!string.IsNullOrEmpty(destinationEntity)
//                     && entityTrees.TryGetValue(fieldMap.DestinationAlias, out var targetEntityTree))
//                 {
//                     var entityNodeKey =
//                         $"{fieldMap.DestinationAlias}~{destinationEntity}~{fieldName.ToUpperCamelCase()}";
//
//                     if (!linkEntityDictionaryTree.TryGetValue(entityNodeKey, out var EntityNodeTo))
//                     {
//                         entityNodeKey =
//                             $"{fieldMap.DestinationAlias}~{destinationEntity}~{fieldMap.DestinationName.ToUpperCamelCase()}";
//                         linkEntityDictionaryTree.TryGetValue(entityNodeKey, out EntityNodeTo);
//                     }
//
//                     if (EntityNodeTo != null)
//                     {
//                         AddField(sqlStatementNodes,
//                             $"{targetEntityTree.Alias}~{targetEntityTree.Name}~{fieldName.ToUpperCamelCase()}",
//                             EntityNodeTo, isEdge);
//
//                         visitedModels.Add(targetEntityTree.Alias);
//                         visitedEntities.Add(ModelNodeFrom.Table);
//                     }
//                 }
//                 else
//                 {
//                     if (linkEntityDictionaryTree.TryGetValue(ModelNodeFrom.RelationshipKey, out var EntityNodeTo))
//                     {
//                         var link = ModelNodeFrom.ModelChildren.FirstOrDefault();
//                         var modelToEntityTree = link != null
//                             ? entityTrees[link.To]
//                             : currentTree;
//
//                         AddField(sqlStatementNodes,
//                             $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{fieldName.ToUpperCamelCase()}",
//                             EntityNodeTo, isEdge);
//                     }
//                 }
//             }
//
//             return;
//         }
//
//         foreach (var childNode in node.GetNodes())
//         {
//             var nameNode = node.GetNodes().FirstOrDefault(a => a.Kind == SyntaxKind.Name);
//
//             if (nameNode != null)
//             {
//                 var name = nameNode.ToString();
//
//                 var matched = trees.Values.FirstOrDefault(t =>
//                     name.Matches(t.ModelName) ||
//                     name.Matches(t.Alias));
//
//                 if (matched != null)
//                     currentTree = entityTrees[matched.Alias];
//             }
//
//             GetFields(trees, entityTrees, childNode,
//                 linkEntityDictionaryTree,
//                 linkModelDictionaryTree,
//                 sqlStatementNodes,
//                 currentTree,
//                 visitedModels,
//                 visitedEntities,
//                 models,
//                 modelEntityNodes,
//                 isEdge);
//         }
//     }
//
//     private static void AddField(
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         string key,
//         EntityNode EntityNode,
//         bool isEdge)
//     {
//         if (EntityNode == null) return;
//
//         var cloned = EntityNode.Clone() as EntityNode;
//         if (cloned == null) return;
//
//         cloned.EntityNodeTypes.Clear();
//         cloned.EntityNodeTypes.Add(isEdge ? EntityNodeType.Edge : EntityNodeType.Node);
//
//         sqlStatementNodes[key] = cloned;
//     }
//
//     // ---------------------------------------------------------------------
//     // FIXED TRAVERSAL (NO STRUCTURAL BREAKAGE)
//     // ---------------------------------------------------------------------
//
//     private static void GenerateQuery(
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         List<Type> entityTypes,
//         Dictionary<string, EntityNode> linkEntityDictionaryTreeNode,
//         StringBuilder sqlQueryStatement,
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         Dictionary<string, string> sqlWhereStatement,
//         Dictionary<string, string> sqlOrderStatement,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree,
//         Dictionary<string, SqlQueryStructure> sqlQueryStructures,
//         Dictionary<string, Type> splitOnDapper,
//         Dictionary<string, Type> removeOnDapper,
//         List<string> entityOrder,
//         List<string> visitedEntities)
//     {
//         if (visitedEntities.Contains(currentTree.Alias))
//             return;
//
//         visitedEntities.Add(currentTree.Alias);
//
//         var currentEntityStructure = GenerateEntityQuery(
//             entityTrees,
//             linkEntityDictionaryTreeNode,
//             sqlStatementNodes,
//             currentTree,
//             sqlQueryStatement,
//             sqlQueryStructures);
//
//         sqlQueryStructures[currentTree.Alias] = currentEntityStructure;
//
//         var children = currentTree.EntityChildren
//             .Concat(currentTree.EntityChildrenRelated);
//
//         foreach (var child in children.DistinctBy(a => a.AliasTo))
//         {
//             if (!entityTrees.TryGetValue(child.AliasTo, out var childTree))
//                 continue;
//
//             GenerateQuery(
//                 entityTrees,
//                 entityTypes,
//                 linkEntityDictionaryTreeNode,
//                 sqlQueryStatement,
//                 sqlStatementNodes,
//                 sqlWhereStatement,
//                 sqlOrderStatement,
//                 childTree,
//                 sqlQueryStructures,
//                 splitOnDapper,
//                 removeOnDapper,
//                 entityOrder,
//                 visitedEntities);
//         }
//     }
//
//     // ---------------------------------------------------------------------
//     // PLACEHOLDERS (YOU DIDN’T WANT DELETED)
//     // ---------------------------------------------------------------------
//
//     private static SqlQueryStructure GenerateEntityQuery(
//         Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityTrees,
//         Dictionary<string, EntityNode> linkEntityDictionaryTreeNode,
//         Dictionary<string, EntityNode> sqlStatementNodes,
//         CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree,
//         StringBuilder sqlQueryStatement,
//         Dictionary<string, SqlQueryStructure> sqlQueryStructures)
//     {
//         return new SqlQueryStructure();
//     }
//
//     private static string GenerateGraphQuery(CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree currentTree, GraphMap graphMap)
//         => string.Empty;
// }