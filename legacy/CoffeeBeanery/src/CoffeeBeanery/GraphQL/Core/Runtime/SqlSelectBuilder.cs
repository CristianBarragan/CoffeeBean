using System.Text;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;
using FASTER.core;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public class SqlSelectBuilder
{
    public static void HandleGraphQL(
        SqlCompilationContext context,
        Dictionary<string, SqlNode> sqlNodes,
        Dictionary<string, SqlNode> sqlNodeStatements,
        Dictionary<string, NodeTree> entityTrees,
        NodeTree rootTree,
        IFasterKV<string, string> cache,
        string cacheKey,
        Dictionary<string, List<string>> permissions = null)
    {
        var sqlQueryStatement = new StringBuilder();
        var sqlQueryStructures = new Dictionary<string, SqlQueryStructure>(
            StringComparer.OrdinalIgnoreCase);
        var splitOnDapper = new Dictionary<string, Type>();
        var removeOnDapper = new Dictionary<string, Type>();
        var entityOrder = new List<string>();

        var entityTypes = entityTrees.Select(a => a.Value.EntityType).ToList();
        var rootEntity = entityTrees.First(a => a.Key.Matches(rootTree.ModelToEntityLinks[0].AliasTo)).Value;
        
        if (!splitOnDapper.ContainsKey($"{rootTree.Alias}~{"Id".ToSnakeCase(rootTree.Id)}"))
        {
            splitOnDapper.Add($"{rootEntity.Alias}~{"Id".ToSnakeCase(rootEntity.Id)}",
                entityTypes.FirstOrDefault(e => e.Name.Matches(rootEntity.Name)));
        }
        
        GenerateQuery(entityTrees, entityTypes, sqlNodes,
            sqlQueryStatement, sqlNodeStatements,
            context.SqlWhereStatement,
            context.SqlOrderStatements,
            rootEntity,
            sqlQueryStructures,
            splitOnDapper, removeOnDapper, entityOrder, new List<string>());

        var queryStructure = sqlQueryStructures.FirstOrDefault();
        var sqlSelectStatement = queryStructure.Value.Query;
        context.SelectSql = sqlSelectStatement;
        context.SplitOnDapper = splitOnDapper;
    }
    
    public static void GetMutations(
        Dictionary<string, NodeTree> trees,
        ISyntaxNode node,
        Dictionary<string, SqlNode> linkModelDictionaryTree,
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes,
        NodeTree currentTree,
        string previousNode,
        List<string> models)
    {
        if (node == null)
            return;

        var nodeName = node.ToString();

        var colonIndex = nodeName.IndexOf(':');
        if (colonIndex > 0)
        {
            var fieldName = nodeName[..colonIndex].Trim();
            var rawValue  = nodeName[(colonIndex + 1)..].Trim().Sanitize().Replace("_", "");

            var lookupKey = $"{currentTree.Alias}~{currentTree.Name}~{fieldName}";

            if (linkModelDictionaryTree.TryGetValue(lookupKey, out var sqlNodeFrom) &&
                linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
            {
                var isEnum = false;

                var enumValue = sqlNodeTo.FromEnumeration
                    .FirstOrDefault(a => a.Key.Matches(rawValue));

                if (!string.IsNullOrEmpty(enumValue.Key))
                {
                    isEnum = true;
                    sqlNodeTo.Value = enumValue.Value.ToString();
                }
                else
                {
                    sqlNodeTo.Value = rawValue;
                }

                AddEntity(linkEntityDictionaryTree, sqlStatementNodes,
                    trees, currentTree, sqlNodeTo, isEnum);

                return;
            }
        }
        
        foreach (var childNode in node.GetNodes())
        {
            var childText = childNode.ToString();

            var nameNode = childNode.Kind == SyntaxKind.Name
                ? childNode
                : childNode.GetNodes().FirstOrDefault(n => n.Kind == SyntaxKind.Name);

            var fieldName = nameNode?.ToString();

            var childTree = currentTree;

            if (!string.IsNullOrEmpty(fieldName))
            {
                var modelMatch = models.FirstOrDefault(m => m.Matches(fieldName));
                if (modelMatch != null)
                {
                    var matched = trees.FirstOrDefault(t =>
                        t.Value.Name.Matches(modelMatch)).Value;
                    if (matched != null)
                        childTree = matched;
                }
                
                if (childTree == currentTree)
                {
                    var byPrefix = trees.Values.FirstOrDefault(t =>
                        !string.IsNullOrEmpty(t.Prefix) &&
                        $"{t.Prefix}{t.ModelName}".Matches(fieldName));
                    if (byPrefix != null)
                        childTree = byPrefix;
                }
                
                if (childTree == currentTree)
                {
                    var byPrefixOnly = trees.Values.FirstOrDefault(t =>
                        !string.IsNullOrEmpty(t.Prefix) &&
                        t.Prefix.Matches(fieldName));
                    if (byPrefixOnly != null)
                        childTree = byPrefixOnly;
                }
                
                if (childTree == currentTree)
                {
                    var byModelName = trees.Values.FirstOrDefault(t =>
                        fieldName.Matches(t.ModelName) ||
                        fieldName.ToUpperCamelCase().Matches(t.ModelName));
                    if (byModelName != null)
                        childTree = byModelName;
                }
                
                if (childTree == currentTree)
                {
                    var byAlias = trees.Values.FirstOrDefault(t =>
                        fieldName.Matches(t.Alias) ||
                        fieldName.ToUpperCamelCase().Matches(t.Alias));
                    if (byAlias != null)
                        childTree = byAlias;
                }
            }

            GetMutations(trees, childNode,
                linkEntityDictionaryTree, linkModelDictionaryTree,
                sqlStatementNodes, childTree, childText, models);
        }
    }
    
    private static void AddEntity(
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes,
        Dictionary<string, NodeTree> entityTrees,
        NodeTree currentTree,
        SqlNode sqlNode,
        bool isEnum)
    {
        if (sqlNode == null) return;

        foreach (var auxFieldMap in currentTree.NodeMap?.FieldMaps
                     .Where(f => f.SourceName.Matches(sqlNode.RelationshipKey.Split('~')[2])) ?? [])
        {
            var entity = sqlNode.Clone() as SqlNode;
            if (entity == null) continue;

            if (string.IsNullOrEmpty(entity.Value))
                entity.Value = sqlNode.Value;

            entity.Alias = auxFieldMap.DestinationAlias;
            entity.Table = currentTree.IsEntity ? currentTree.Name : auxFieldMap.DestinationEntity;
            entity.RelationshipKey =
                $"{(currentTree.IsEntity ? currentTree.Alias : auxFieldMap.DestinationAlias)}~" +
                $"{(currentTree.IsEntity ? currentTree.Name : auxFieldMap.DestinationEntity)}~" +
                $"{sqlNode.RelationshipKey.Split('~')[2]}";
            entity.FromComplexModel = !currentTree.IsEntity;

            if (!sqlStatementNodes.ContainsKey(entity.RelationshipKey))
                sqlStatementNodes.Add(entity.RelationshipKey, entity);
        }
    }

    public static void GetFields(
        Dictionary<string, NodeTree> trees,
        Dictionary<string, NodeTree> entityTrees,
        ISyntaxNode node,
        Dictionary<string, SqlNode> linkEntityDictionaryTree,
        Dictionary<string, SqlNode> linkModelDictionaryTree,
        Dictionary<string, SqlNode> sqlStatementNodes,
        NodeTree currentTree,
        List<string> visitedModels,
        List<string> visitedEntities,
        List<string> models,
        Dictionary<string, SqlNode> modelSqlNodes,
        bool isEdge)
    {
        if (node != null && node.GetNodes()?.Count() == 0)
        {
            var fieldName = node.ToString().Trim();

            if (linkModelDictionaryTree.TryGetValue(
                    $"{currentTree.Alias}~{currentTree.Name}~{fieldName}",
                    out var sqlNodeFrom))
            {
                modelSqlNodes[$"{currentTree.Alias}~{currentTree.Name}~{fieldName}"] = sqlNodeFrom;

                var fieldMap = currentTree.NodeMap?.FieldMaps
                    .FirstOrDefault(f => f.SourceName.Matches(fieldName));

                var destinationEntity = fieldMap?.DestinationEntity;

                if (!string.IsNullOrEmpty(destinationEntity)
                    && entityTrees.TryGetValue(fieldMap.DestinationAlias, out var targetEntityTree))
                {
                    var entityNodeKey =
                        $"{fieldMap.DestinationAlias}~{destinationEntity}~{fieldName.ToUpperCamelCase()}";

                    if (!linkEntityDictionaryTree.TryGetValue(entityNodeKey, out var sqlNodeTo))
                    {
                        entityNodeKey =
                            $"{fieldMap.DestinationAlias}~{destinationEntity}~{fieldMap.DestinationName.ToUpperCamelCase()}";
                        linkEntityDictionaryTree.TryGetValue(entityNodeKey, out sqlNodeTo);
                    }

                    if (sqlNodeTo != null)
                    {
                        AddField(sqlStatementNodes,
                            $"{targetEntityTree.Alias}~{targetEntityTree.Name}~{fieldName.ToUpperCamelCase()}",
                            sqlNodeTo, isEdge);

                        if (!visitedModels.Contains(targetEntityTree.Alias))
                            visitedModels.Add(targetEntityTree.Alias);

                        visitedEntities.Add(sqlNodeFrom.Table);
                    }
                }
                else
                {
                    if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                    {
                        var nodeEntityTree = sqlNodeFrom.LinkKeys
                            .First(b => b.FromColumn.Matches(sqlNodeFrom.RelationshipKey.Split('~')[2]));

                        var modelToEntityTree = entityTrees[nodeEntityTree.AliasTo];

                        AddField(sqlStatementNodes,
                            $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{fieldName.ToUpperCamelCase()}",
                            sqlNodeTo, isEdge);

                        if (!visitedModels.Contains(currentTree.Alias))
                            visitedModels.Add(currentTree.Alias);
                    }

                    visitedEntities.Add(sqlNodeFrom.Table);
                }
            }

            return;
        }

        if (node == null) return;

        foreach (var childNode in node.GetNodes())
        {
            var childName = node.GetNodes().FirstOrDefault(a => a.Kind == SyntaxKind.Name);

            if (childName != null && models.Any(e => e.Matches(childName.ToString()))
                || childNode.ToString().Matches("nodes")
                || childNode.ToString().Matches("node"))
            {
                currentTree = childNode.ToString().Matches("nodes") || childNode.ToString().Matches("node")
                    ? trees.OrderBy(a => a.Value.Id).First().Value
                    : trees.First(a => a.Value.Name.Matches(childName.ToString())).Value;
            }
            else if (childName != null
                && trees.FirstOrDefault(a =>
                    a.Value.ModelName.Matches(childName.ToString().ToUpperCamelCase())).Value != null)
            {
                currentTree = trees.First(a =>
                    a.Value.ModelName.Matches(childName.ToString().ToUpperCamelCase())).Value;
            }

            GetFields(trees, entityTrees, childNode,
                linkEntityDictionaryTree, linkModelDictionaryTree,
                sqlStatementNodes, currentTree, visitedModels, visitedEntities,
                models, modelSqlNodes, isEdge);
        }
    }

    private static void AddField(
        Dictionary<string, SqlNode> sqlStatementNodes,
        string key,
        SqlNode sqlNode,
        bool isEdge)
    {
        if (sqlNode == null) return;

        var cloned = sqlNode.Clone() as SqlNode;
        if (cloned == null) return;

        cloned.SqlNodeTypes.Clear();
        cloned.SqlNodeTypes.Add(isEdge ? SqlNodeType.Edge : SqlNodeType.Node);
        sqlStatementNodes[key] = cloned;
    }
    
    private static string GenerateGraphQuery(
        NodeTree currentTree,
        GraphMap graphMap)
    {
        if (graphMap == null)
            return string.Empty;

        var edgeReturn = string.IsNullOrEmpty(graphMap.EdgeKeyColumn)
            ? ""
            : $"r.{graphMap.EdgeKeyColumn}";

        var edgeColumnDef = string.IsNullOrEmpty(graphMap.EdgeKeyColumn)
            ? ""
            : $"{graphMap.EdgeKeyColumn} agtype";

        var edgeSelect = string.IsNullOrEmpty(graphMap.EdgeKeyColumn)
            ? ""
            : $"({graphMap.EdgeKeyColumn})::text::uuid AS {graphMap.EdgeKeyColumn}";

        return @$"
            WITH graph_edges AS
            (
                SELECT DISTINCT *
                FROM cypher(
                    '{graphMap.GraphName}',
                    $$

                    MATCH (a:{graphMap.FromVertex.Label})-[r:{graphMap.EdgeLabel}]->(b:{graphMap.ToVertex.Label})

                    RETURN
                        a.properties.{graphMap.FromVertex.KeyColumn} AS from_key,
                        b.properties.{graphMap.ToVertex.KeyColumn} AS to_key,
                        {edgeReturn}

                    $$
                ) AS
                (
                    from_key agtype,
                    to_key agtype,
                    {edgeColumnDef.TrimEnd(',')}
                )
            )

            SELECT
                from_key::text::{GetColumnType(graphMap.FromVertex.KeyType)} AS {graphMap.FromVertex.KeyColumn},
                to_key::text::{GetColumnType(graphMap.ToVertex.KeyType)} AS {graphMap.ToVertex.KeyColumn},
                {edgeSelect.TrimEnd(',')}

            FROM graph_edges
            ";
    }
    
    private static void GenerateQuery(
        Dictionary<string, NodeTree> entityTrees,
        List<Type> entityTypes,
        Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
        StringBuilder sqlQueryStatement,
        Dictionary<string, SqlNode> sqlStatementNodes,
        Dictionary<string, string> sqlWhereStatement,
        Dictionary<string, string> sqlOrderStatement,
        NodeTree currentTree,
        Dictionary<string, SqlQueryStructure> sqlQueryStructures,
        Dictionary<string, Type> splitOnDapper,
        Dictionary<string, Type> removeOnDapper,
        List<string> entityOrder,
        List<string> visitedEntities)
    {
        if (visitedEntities.Contains(currentTree.Alias)) return;
        visitedEntities.Add(currentTree.Alias);

        var currentEntityStructure = GenerateEntityQuery(
            entityTrees,
            linkEntityDictionaryTreeNode,
            sqlStatementNodes,
            currentTree,
            sqlQueryStatement,
            sqlQueryStructures);

        if (!sqlQueryStructures.Any(a => a.Key.Matches(currentTree.Alias)))
            sqlQueryStructures.Add(currentTree.Alias, currentEntityStructure);
        
        var queryBuilder =
            $"SELECT DISTINCT % FROM \"{currentTree.Schema}\".\"{currentTree.Name}\" {currentTree.Alias} ";

        currentEntityStructure.SelectColumns.AddRange(currentEntityStructure.Columns);
        currentEntityStructure.SelectColumns =
            currentEntityStructure.SelectColumns.Distinct().ToList();
        currentEntityStructure.HasChildren = true;

        currentTree = entityTrees[currentTree.Alias];

        if (!entityOrder.Contains(currentTree.Name))
            entityOrder.Add(currentTree.Name);

        foreach (var child in currentTree.Children
                     .Concat(currentTree.RelatedChildren)
                     .DistinctBy(a => a.AliasTo))
        {
            var childTree = entityTrees[child.AliasTo];

            GenerateQuery(entityTrees, entityTypes, linkEntityDictionaryTreeNode,
                sqlQueryStatement, sqlStatementNodes, sqlWhereStatement, sqlOrderStatement,
                childTree, sqlQueryStructures,
                splitOnDapper, removeOnDapper, entityOrder, visitedEntities);

            if (!(sqlQueryStructures.Any(a => a.Key.Matches(childTree.Alias) &&
                                              sqlQueryStructures[childTree.Alias].Columns.Count - 1
                                              > sqlQueryStructures[childTree.Alias].ChildrenJoinColumns.Count))
                && !entityTrees.Any(b => b.Value.Parents.Any(c => c.AliasTo.Matches(childTree.Alias))))
            {
                if (!removeOnDapper.ContainsKey("Id".ToSnakeCase(childTree.Id)))
                    removeOnDapper.Add("Id".ToSnakeCase(childTree.Id),
                        entityTypes.FirstOrDefault(e => e.Name.Matches(childTree.Name)));
                continue;
            }

            var childStructure = sqlQueryStructures[childTree.Alias];

            if (childStructure.Columns.Count == 0) continue;

            childStructure.HasChildren = true;

            if (!string.IsNullOrEmpty(childStructure.GraphQuery))
            {
                queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge
                    ? " JOIN "
                    : " LEFT JOIN ";

                var linkToChildForGraph = childTree.Parents
                    .FirstOrDefault(a => a.To.Matches(currentTree.Name))
                    ?? childTree.RelatedParents
                        .FirstOrDefault(a => a.To.Matches(currentTree.Name));

                queryBuilder +=
                    $" ( {childStructure.GraphQuery} )) {childTree.Alias}_graph" +
                    $" ON {currentTree.Alias}.\"{linkToChildForGraph.ToColumn}\"" +
                    $" = {childTree.Alias}_graph.\"{linkToChildForGraph.FromColumn.ToSnakeCase(childTree.Id)}\"";
            }

            queryBuilder += childStructure.SqlNodeType == SqlNodeType.Edge
                ? " JOIN "
                : " LEFT JOIN ";

            var linkToChild = childTree.Parents.FirstOrDefault(a => a.AliasTo.Matches(currentTree.Alias))
                              ?? childTree.RelatedParents.FirstOrDefault(a =>
                                  a.AliasTo.Matches(currentTree.Alias));

            queryBuilder +=
                $" ( {childStructure.Query} ) {childTree.Alias}" +
                $" ON {currentTree.Alias}.\"{linkToChild.ToColumn}\"" +
                $" = {childTree.Alias}.\"{linkToChild.FromColumn.ToSnakeCase(childTree.Id)}\"";

            if (!splitOnDapper.ContainsKey($"{childTree.Alias}~{"Id".ToSnakeCase(childTree.Id)}"))
            {
                splitOnDapper.Add($"{childTree.Alias}~{"Id".ToSnakeCase(childTree.Id)}",
                    entityTypes.FirstOrDefault(e => e.Name.Matches(childTree.Name)));
            }
            
            currentEntityStructure.SelectColumns.AddRange(
                childStructure.ParentColumns.Select(s => s.Replace("~", childTree.Alias)));
            currentEntityStructure.ParentColumns.AddRange(childStructure.ParentColumns);
            currentEntityStructure.SelectColumns =
                currentEntityStructure.SelectColumns.Distinct().ToList();
            currentEntityStructure.ParentColumns =
                currentEntityStructure.ParentColumns.Distinct().ToList();
        }

        var select = string.Join(",",
            currentEntityStructure.SelectColumns.DistinctBy(a => a.Split(" AS ")[1]));

        if (string.IsNullOrEmpty(select)) return;
        
        if (!string.IsNullOrEmpty(currentEntityStructure.GraphQuery))
        {
            queryBuilder += currentEntityStructure.SqlNodeType == SqlNodeType.Edge
                ? " JOIN "
                : " LEFT JOIN ";

            queryBuilder +=
                $" ( {currentEntityStructure.GraphQuery} ) {currentTree.Alias}_graph" +
                $" ON {currentTree.Alias}.\"{currentTree.GraphMap.EdgeKeyColumn}\"" +
                $" = {currentTree.Alias}_graph.{currentTree.GraphMap.EdgeKeyColumn}";
        }
        
        if (sqlWhereStatement.TryGetValue(currentTree.Alias, out var whereClause))
        {
            queryBuilder += " WHERE " + whereClause.Replace("~", currentTree.Alias);
        }
        
        if (sqlOrderStatement.TryGetValue(currentTree.Alias, out var orderClause))
        {
            queryBuilder += " ORDER BY " + orderClause.Replace("~*~", currentTree.Alias);
        }

        queryBuilder = queryBuilder.Replace("%", select);
        currentEntityStructure.Query = queryBuilder;

        var currentNode = linkEntityDictionaryTreeNode
            .FirstOrDefault(a => a.Key.Contains(currentTree.Alias));
        currentEntityStructure.Id = currentTree.Id;
        currentEntityStructure.SqlNode = currentNode.Value;

        if (sqlQueryStructures.TryGetValue(currentTree.Alias, out _))
            sqlQueryStructures[currentTree.Alias] = currentEntityStructure;
        else
            sqlQueryStructures.Add(currentTree.Alias, currentEntityStructure);
    }
    
    private static SqlQueryStructure GenerateEntityQuery(
        Dictionary<string, NodeTree> entityTrees,
        Dictionary<string, SqlNode> linkEntityDictionaryTreeNode,
        Dictionary<string, SqlNode> sqlStatementNodes,
        NodeTree currentTree,
        StringBuilder sqlQueryStatement,
        Dictionary<string, SqlQueryStructure> sqlQueryStructures)
    {
        var childrenJoinColumns = new Dictionary<string, string>();
        var currentColumns = new List<KeyValuePair<string, SqlNode>>();

        var columnToAdd = linkEntityDictionaryTreeNode
            .FirstOrDefault(k => k.Key.Matches($"{currentTree.Alias}~{currentTree.Name}~Id"));
        
        if (columnToAdd.Value == null) return new SqlQueryStructure();

        columnToAdd.Value.SqlNodeTypes = new List<SqlNodeType> { SqlNodeType.Node };
        currentColumns.Add(columnToAdd);

        currentColumns.AddRange(sqlStatementNodes
            .Where(k => k.Key.Split('~')[0].Matches(currentTree.Alias)));
        currentColumns = currentColumns.Distinct().ToList();

        if (currentColumns.Count > 1
            && !currentColumns.First().Value.LinkKeys
                .Where(a => a.ToColumn.Matches("Id"))
                .Any(a => !sqlStatementNodes.Any(b => b.Value.Column.Matches(a.FromColumn))))
        {
            return new SqlQueryStructure();
        }

        var queryColumns = new List<string>();
        var parentQueryColumns = new List<string>();

        foreach (var tableColumn in currentColumns)
        {
            var fieldName = tableColumn.Value.Column;
            var colExpr =
                $"{currentTree.Alias}.\"{fieldName}\" AS \"{fieldName.ToSnakeCase(currentTree.Id)}\"";

            if (queryColumns.Contains(colExpr)) continue;

            queryColumns.Add(colExpr);
            parentQueryColumns.Add(
                $"~.\"{fieldName.ToSnakeCase(currentTree.Id)}\" AS \"{fieldName.ToSnakeCase(currentTree.Id)}\"");

            foreach (var linkKey in Enumerable
                         .Concat(currentTree.Parents, currentTree.RelatedParents)
                         .Where(a => a.ToColumn.Matches("Id")))
            {
                if (!sqlQueryStructures.Any(s => s.Key.Matches(linkKey.AliasTo))) continue;
                if (childrenJoinColumns.ContainsKey(linkKey.AliasTo)) continue;

                childrenJoinColumns.Add(linkKey.AliasTo, linkKey.ToColumn);

                var fromColExpr =
                    $"{currentTree.Alias}.\"{linkKey.FromColumn}\" AS \"{linkKey.FromColumn.ToSnakeCase(currentTree.Id)}\"";
                if (!queryColumns.Contains(fromColExpr))
                    queryColumns.Add(fromColExpr);

                var fromColParentExpr =
                    $"~.\"{linkKey.FromColumn.ToSnakeCase(currentTree.Id)}\" AS \"{linkKey.FromColumn.ToSnakeCase(currentTree.Id)}\"";
                if (!parentQueryColumns.Contains(fromColParentExpr))
                    parentQueryColumns.Add(fromColParentExpr);
            }
        }

        var currentJoinOnKeys = currentColumns
            .FirstOrDefault(a => a.Key.Split('~')[0].Matches(currentTree.Alias)).Value;

        var oneKey = currentJoinOnKeys.EntityRelatedChildren.FirstOrDefault()
                     ?? currentJoinOnKeys.EntityChildren.FirstOrDefault();

        if (oneKey != null
            && !childrenJoinColumns.ContainsKey(currentJoinOnKeys.RelationshipKey.Split('~')[0]))
        {
            if (!childrenJoinColumns.ContainsValue(oneKey.ToColumn))
                childrenJoinColumns.Add(currentJoinOnKeys.RelationshipKey.Split('~')[0],
                    oneKey.ToColumn);

            var joinOnKey =
                $"{currentTree.Alias}.\"{oneKey.FromColumn}\" AS \"{oneKey.FromColumn.ToSnakeCase(currentTree.Id)}\"";
            var joinOneKeyParent =
                $"~.\"{oneKey.FromColumn.ToSnakeCase(currentTree.Id)}\" AS \"{oneKey.FromColumn.ToSnakeCase(currentTree.Id)}\"";

            queryColumns.Add(joinOnKey);
            parentQueryColumns.Add(joinOneKeyParent);
        }

        if (queryColumns.Count == 0) return new SqlQueryStructure();

        queryColumns = queryColumns.Distinct().ToList();
        
        var graphSql = currentTree.GraphMap != null
            ? GenerateGraphQuery(currentTree, currentTree.GraphMap)
            : string.Empty;

        return new SqlQueryStructure
        {
            Id = currentTree.Id,
            Name = currentTree.Name,
            SqlNodeType = currentColumns.Count > 1 && currentColumns.Last().Value.SqlNodeTypes.Count > 0
                ? currentColumns.Last().Value.SqlNodeTypes[0]
                : SqlNodeType.Node,
            SqlNode = currentColumns.Count > 0
                ? currentColumns.Last().Value
                : new SqlNode(),
            Query = string.Empty,
            GraphQuery = graphSql,
            Columns = queryColumns,
            ParentColumns = parentQueryColumns,
            ChildrenJoinColumns = childrenJoinColumns
        };
    }
    
    private static string GetColumnType(string propertyType)
    {
        return propertyType?.ToUpperInvariant() switch
        {
            "GUID" => "uuid",
            "UUID" => "uuid",
            "INT" => "int",
            "INTEGER" => "int",
            "LONG" => "bigint",
            "BIGINT" => "bigint",
            "STRING" => "text",
            _ => "text"
        };
    }
}