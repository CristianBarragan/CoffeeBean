using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public static class SqlNodeResolver
    {
        public static void GetMutations(
            Dictionary<string, NodeTree> trees,
            Dictionary<string, NodeTree> entityTrees,
            ISyntaxNode node,
            Dictionary<string, SqlNode> linkEntityDictionaryTree,
            Dictionary<string, SqlNode> linkModelDictionaryTree,
            Dictionary<string, SqlNode> sqlStatementNodes,
            NodeTree currentTree,
            string previousNode,
            NodeTree parentTree,
            List<string> modelNames,
            List<string> entityNames,
            List<string> visitedModels)
        {
            if (node != null && node.GetNodes()?.Count() == 0)
            {
                if (linkModelDictionaryTree.TryGetValue(
                        $"{currentTree.Alias}~{currentTree.Name}~{node.ToString()}",
                        out var sqlNodeFrom))
                {
                    if (linkEntityDictionaryTree.TryGetValue(sqlNodeFrom.RelationshipKey,
                            out var sqlNodeTo))
                    {
                        var modelToEntityTree = entityTrees.FirstOrDefault(a => 
                            a.Value.Alias.Matches(sqlNodeFrom.RelationshipKey.Split('~')[0]) ||
                            a.Value.Alias.Matches(sqlNodeFrom.RelationshipKey.Split('~')[1]) ||
                            a.Value.Alias.Matches(sqlNodeFrom.Table) ||
                            sqlNodeFrom.LinkKeys.Any( b=> b.To.Matches(a.Value.Name))).Value;
                        
                        modelToEntityTree = !currentTree.IsEntity ? modelToEntityTree : currentTree;

                        HandleEntityNode(sqlNodeTo, previousNode, linkModelDictionaryTree, currentTree,
                            node, sqlStatementNodes, trees, linkEntityDictionaryTree, $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{node.ToString()}"
                            , $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{
                                node.ToString()}",
                            node.ToString().Split(':')[0]);
                        
                        foreach (var linkKey in modelToEntityTree.ModelToEntityLinks.Where(a => a.FromColumn.Matches(node.ToString().Split(':')[0])))
                        {
                            var entityTreeFrom = entityTrees[linkKey.From];
                            var entityTreeTo = entityTrees.FirstOrDefault(a => 
                                a.Value.Name.Matches(sqlNodeFrom.Table) ||
                                a.Value.Name.Matches(linkKey.To)).Value;

                            foreach (var linkKeyChildren in entityTreeFrom.Children)
                            {
                                var linkKeyChildrenTo = entityTreeTo.ModelToEntityLinks.FirstOrDefault(a => a
                                    .To.Matches(linkKey.To));

                                if (linkKeyChildrenTo == null)
                                {
                                    break;
                                }
                                
                                if (linkEntityDictionaryTree.TryGetValue($"{entityTreeFrom.Alias}~{linkKeyChildren.From}~{
                                    linkKeyChildrenTo.ToColumn}",
                                        out sqlNodeTo))
                                {
                                    HandleEntityNode(sqlNodeTo, previousNode, linkModelDictionaryTree, currentTree,
                                        node, sqlStatementNodes, trees, linkEntityDictionaryTree, $"{entityTreeFrom.Alias}~{linkKeyChildren.From}~{
                                            linkKeyChildrenTo.ToColumn}", $"{linkKeyChildren.From}~{linkKeyChildren.From}~{
                                                linkKeyChildrenTo.ToColumn}", node.ToString().Split(':')[0]);
                                }    
                            }
                            
                            foreach (var linkKeyChildren in entityTreeFrom.RelatedChildren)
                            {
                                var linkKeyChildrenTo = entityTreeTo.ModelToEntityLinks.FirstOrDefault(a => a
                                    .To.Matches(linkKey.To));

                                if (linkKeyChildrenTo == null)
                                {
                                    break;
                                }
                                
                                if (linkEntityDictionaryTree.TryGetValue($"{entityTreeFrom.Alias}~{linkKeyChildren.From}~{
                                    linkKeyChildrenTo.ToColumn}",
                                        out sqlNodeTo))
                                {
                                    HandleEntityNode(sqlNodeTo, previousNode, linkModelDictionaryTree, currentTree,
                                        node, sqlStatementNodes, trees, linkEntityDictionaryTree, $"{entityTreeFrom.Alias}~{linkKeyChildren.From}~{
                                            linkKeyChildrenTo.ToColumn}", $"{linkKeyChildren.From}~{linkKeyChildren.From}~{
                                                linkKeyChildrenTo.ToColumn}", linkKeyChildrenTo.FromColumn);
                                }    
                            }
                        }
                    }
                }

                if (!visitedModels.Contains(currentTree.Name))
                    visitedModels.Add(currentTree.Name);

                return;
            }

            if (node == null)
                return;

            foreach (var childNode in node.GetNodes())
            {
                if (modelNames.Any(e => e.Matches(childNode.ToString().Split('{')[0])) ||
                    node.ToString().Matches("nodes") ||
                    node.ToString().Matches("node"))
                {
                    if (node.ToString().Matches("nodes") || node.ToString().Matches("node"))
                        currentTree = trees[modelNames.Last()];
                    else
                        currentTree = trees[childNode.ToString().Split('{')[0]];

                    parentTree = currentTree.Parents.Count == 0
                        ? currentTree
                        : trees[currentTree.Parents[0].To];
                }

                GetMutations(trees, entityTrees, childNode, linkEntityDictionaryTree, linkModelDictionaryTree,
                    sqlStatementNodes, currentTree, node.ToString(),
                    parentTree, modelNames, entityNames, visitedModels);
            }
        }

        private static void HandleEntityNode(SqlNode sqlNodeTo, string previousNode, 
            Dictionary<string, SqlNode> linkModelDictionaryTree, NodeTree currentTree, ISyntaxNode node,
            Dictionary<string, SqlNode> sqlStatementNodes, Dictionary<string, NodeTree> trees,
            Dictionary<string, SqlNode> linkEntityDictionaryTree, string key, string keyTo, string column)
        {
            var value = string.Empty;

            if (previousNode.Split(':').Length == 2)
            {
                var enumKeyValue = sqlNodeTo.FromEnumeration.FirstOrDefault(a => a.Key
                    .Matches(key));
                
                if (String.IsNullOrEmpty(enumKeyValue.Key))
                {
                    var keyParts = key.Split('~');
                    sqlNodeTo.Column = previousNode.Split(':')[0].ToUpperCamelCase();
                    key = $"{keyParts[0]}~{keyParts[1]}~{sqlNodeTo.Column}";
                    sqlNodeTo.RelationshipKey = key;
                    value = enumKeyValue.Value.ToString();
                }
                else
                {
                    value = previousNode.Split(':')[1].Sanitize();
                }
            }

            AddEntity(sqlStatementNodes, sqlNodeTo,
                key, value);
        }

        private static void AddEntity(
            Dictionary<string, SqlNode> sqlStatementNodes,
            SqlNode sqlNodeTo,
            string key,
            string value)
        {
            if (sqlStatementNodes.ContainsKey(sqlNodeTo.RelationshipKey))
                return;

            var cloned = sqlNodeTo.Clone() as SqlNode;
            cloned.Value = value;
            
            sqlStatementNodes[key] = cloned;
        }

        /// <summary>
        /// Method for getting upsert field information used for the SQL Statement
        /// </summary>
        public static void GetFields(
            Dictionary<string, NodeTree> trees,
            Dictionary<string, NodeTree> entityTrees,
            ISyntaxNode node,
            Dictionary<string, SqlNode> linkEntityDictionaryTree,
            Dictionary<string, SqlNode> linkModelDictionaryTree,
            Dictionary<string, SqlNode> sqlStatementNodes,
            NodeTree currentTree,
            NodeTree parentTree,
            List<string> visitedModels,
            List<string> visitedEntities,
            List<string> models,
            List<string> entities,
            bool isEdge)
        {
            if (node != null && node.GetNodes()?.Count() == 0)
            {
                // var currentModel = visitedModels.LastOrDefault();

                var modelTree = trees.FirstOrDefault(a =>
                    a.Value.Mapping.Any(b =>
                        b.DestinationEntity.Matches(currentTree.Name) &&
                        b.DestinationName.Matches(node.ToString())));

                if (trees.TryGetValue(node.ToString(), out var currentModel))
                {
                    visitedModels.Add(currentModel.Alias);
                    currentTree = currentModel;
                }
                else if (visitedModels.Count > 0)
                {
                    currentTree = trees.First(a => a.Value.Alias.Matches(visitedModels.Last())).Value;
                }

                if (linkModelDictionaryTree.TryGetValue(
                        $"{currentTree.Alias}~{currentTree.Name}~{node.ToString()}",
                        out var sqlNodeFrom))
                {
                    if (linkEntityDictionaryTree.TryGetValue(
                            sqlNodeFrom.RelationshipKey, out var sqlNodeTo))
                    {
                        var modelToEntityTree = entityTrees.FirstOrDefault(a => 
                            a.Value.Alias.Matches(sqlNodeFrom.RelationshipKey.Split('~')[0]) ||
                            a.Value.Alias.Matches(sqlNodeFrom.RelationshipKey.Split('~')[1]) ||
                            a.Value.Alias.Matches(sqlNodeFrom.Table) ||
                            sqlNodeFrom.LinkKeys.Any( b=> b.To.Matches(a.Value.Name))).Value;
                        
                        modelToEntityTree = !currentTree.IsEntity ? modelToEntityTree : currentTree;
                        
                        AddField(linkEntityDictionaryTree, sqlStatementNodes, modelToEntityTree, $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{node.ToString()}", 
                            $"{modelToEntityTree.Alias}~{modelToEntityTree.Name}~{
                                node.ToString()}", sqlNodeTo, isEdge);

                        if (!visitedModels.Contains(currentTree.Alias))
                        {
                            visitedModels.Add(currentTree.Alias);
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        foreach (var modelLinked in currentTree.ModelToEntityLinks)
                        {
                            if (linkEntityDictionaryTree.TryGetValue(
                                    $"{modelLinked.To}~{modelLinked.To}~{node.ToString()}", out sqlNodeTo))
                            {
                                var entityTree = entityTrees[modelLinked.From];
                                
                                AddField(linkEntityDictionaryTree, sqlStatementNodes,
                                    currentTree, $"{entityTree.Alias}~{entityTree.Name}~{node.ToString()}", 
                                    $"{entityTree.Alias}~{entityTree.Name}~{
                                        node.ToString()}",
                                    sqlNodeTo, isEdge);

                                if (!visitedModels.Contains(currentTree.Alias))
                                    visitedModels.Add(currentTree.Alias);
                            }
                        }   
                    }

                    visitedEntities.Add(sqlNodeFrom.Table);
                }

                return;
            }

            if (node == null)
                return;
            
            foreach (var childNode in node.GetNodes())
            {
                if (models.Any(e => e.Matches(childNode.ToString().Split('{')[0])) ||
                    node.ToString().Matches("nodes") ||
                    node.ToString().Matches("node"))
                {
                    if (!(node.ToString().Matches("nodes") || node.ToString().Matches("node")))
                        currentTree = trees.First(a => a.Value.Name.Matches(childNode.ToString().Split('{')[0])).Value;

                    parentTree = currentTree.Parents.Count == 0
                        ? currentTree
                        : trees.First(a =>
                            a.Value.Name.Matches(currentTree.Parents[0].To) &&
                            !visitedModels.Contains(a.Key)).Value;
                }

                GetFields(trees, entityTrees, childNode,
                    linkEntityDictionaryTree, linkModelDictionaryTree,
                    sqlStatementNodes, currentTree, parentTree,
                    visitedModels, visitedEntities, models, entities, isEdge);
            }
        }

        private static void AddField(
            Dictionary<string, SqlNode> linkEntityDictionaryTree,
            Dictionary<string, SqlNode> sqlStatementNodes,
            NodeTree currentTree,
            string key, string keyTo, 
            SqlNode? sqlNode,
            bool isEdge)
        {
            var cloned = sqlNode.Clone() as SqlNode;
            cloned.SqlNodeTypes.Clear();
            cloned.SqlNodeTypes.Add(isEdge ? SqlNodeType.Edge : SqlNodeType.Node);

            if (!sqlStatementNodes.ContainsKey(key))
                sqlStatementNodes.Add(key, cloned);
        }
    }
}