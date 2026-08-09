using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    internal static class SqlOrderCompiler
    {
        public static void Compile(
            SqlCompilationContext context,
            Dictionary<string, NodeTree> trees,
            ISyntaxNode orderNode,
            NodeTree entity,
            Dictionary<string, SqlNode> modelNodes,
            Dictionary<string, NodeTree> entityTrees) 
        {
            var sqlOrderStatement = new Dictionary<string, string>();
            
            GetFieldsOrdering(trees, orderNode, entity, modelNodes, entityTrees, sqlOrderStatement);
            
            context.SqlOrderStatements = sqlOrderStatement;
        }

        private static void GetFieldsOrdering(
            Dictionary<string, NodeTree> modelTrees,
            ISyntaxNode orderNode,
            NodeTree currentEntityTree,
            Dictionary<string, SqlNode> modelNodes,
            Dictionary<string, NodeTree> entityTrees,
            Dictionary<string, string> sqlOrderStatement)
        {
            foreach (var oNode in orderNode.GetNodes())
            {
                var activeEntityTree = currentEntityTree;
                var oNodeStr = oNode.ToString();

                if (oNodeStr.Contains("{") && oNodeStr[0] != '{' && oNodeStr.Contains(":"))
                {
                    var entityName = oNodeStr.Split(":")[0].Trim();
                    var matched = modelTrees.Values.FirstOrDefault(t =>
                        t.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
                        t.Alias.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Equals(entityName.Replace("_", ""), StringComparison.OrdinalIgnoreCase) ||
                        t.Alias.Equals(entityName.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

                    if (matched != null)
                        activeEntityTree = matched;
                }

                if (!oNodeStr.Contains("{") && oNodeStr.Contains(":"))
                {
                    var parts = oNodeStr.Split(":");
                    var field = parts[0].Trim();
                    var direction = parts[1].Trim();

                    if (direction.Contains("DESC") || direction.Contains("ASC"))
                    {
                        var sortResult = HandleSort(activeEntityTree, field, direction, modelNodes, entityTrees);
                        if (sortResult.Value != null)
                        {
                            if (sqlOrderStatement.TryGetValue(sortResult.Key, out var existing))
                                sqlOrderStatement[sortResult.Key] = existing + ", " + sortResult.Value;
                            else
                                sqlOrderStatement[sortResult.Key] = sortResult.Value;
                        }
                    }
                }

                GetFieldsOrdering(modelTrees, oNode, activeEntityTree, modelNodes, entityTrees, sqlOrderStatement);
            }
        }

        private static KeyValuePair<string, string> HandleSort(
            NodeTree nodeTree,
            string field,
            string sortClause,
            Dictionary<string, SqlNode> modelNodes,
            Dictionary<string, NodeTree> entityTrees)
        {
            var linkKeys = nodeTree.ModelToEntityLinks.Where(x =>
                entityTrees[x.AliasTo].Mapping.Any(a => a.DestinationName.Matches(field)));

            if (linkKeys.GetEnumerator().Current == null)
            {
                return new KeyValuePair<string, string>();
            }

            NodeTree entityTree;

            foreach (var linkKey in linkKeys)
            {
                entityTree = entityTrees[linkKey.AliasTo];
                return new KeyValuePair<string, string>(entityTree.Alias, $"~*~.\"{field.ToUpperCamelCase().ToSnakeCase(entityTree.Id)}\" {sortClause.Trim()}");
            }

            if (!entityTrees.ContainsKey(nodeTree.Alias))
            {
                return new KeyValuePair<string, string>();
            }

            entityTree = entityTrees[nodeTree.Alias];
            
            var match = modelNodes.FirstOrDefault(kvp =>
                kvp.Key.StartsWith(nodeTree.Alias, StringComparison.OrdinalIgnoreCase) &&
                kvp.Key.EndsWith($"~{field}", StringComparison.OrdinalIgnoreCase));

            if (match.Value != null)
                return new KeyValuePair<string, string>(entityTree.Alias, $"~*~.\"{match.Value.Column.ToSnakeCase(entityTree.Id)}\" {sortClause.Trim()}");
            
            return new KeyValuePair<string, string>();
        }
    }
}