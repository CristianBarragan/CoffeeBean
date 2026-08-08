using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Helper;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    internal static class SqlWhereCompiler
    {
        public static void Compile(
            SqlCompilationContext context,
            Dictionary<string, SqlNode> modelsSqlNodes,
            Dictionary<string, SqlNode> entitiesSqlNodes,
            ISelection selection,
            NodeTree rootTree,
            string wrapperEntityName,
            Dictionary<string, string> sqlWhereStatement)
        {
            GetFieldsWhere(
                rootTree,
                modelsSqlNodes,
                entitiesSqlNodes,
                sqlWhereStatement,
                selection.SyntaxNode.Arguments.FirstOrDefault(a => a.Name.Value.Matches("where")),
                SqlNodeRegistry.ModelTrees.Last().Value.Name,
                wrapperEntityName,
                string.Empty,
                Entity.ClauseTypes,
                default);

            context.SqlWhereStatement = sqlWhereStatement;
        }

        public static void GetFieldsWhere(
            NodeTree rootTree,
            Dictionary<string, SqlNode> modelsSqlNodes,
            Dictionary<string, SqlNode> entitiesSqlNodes,
            Dictionary<string, string> sqlWhereStatement,
            ISyntaxNode whereNode,
            string rootEntityName,
            string wrapperEntityName,
            string clauseCondition,
            List<string> clauseType,
            Dictionary<string, List<string>> permission = null)
        {
            GetFieldsWhereRecursive(
                rootTree,
                modelsSqlNodes,
                entitiesSqlNodes,
                sqlWhereStatement,
                whereNode,
                rootEntityName,
                rootEntityName,
                wrapperEntityName,
                clauseCondition,
                clauseType,
                permission);
        }

        private static void GetFieldsWhereRecursive(
            NodeTree rootTree,
            Dictionary<string, SqlNode> modelsSqlNodes,
            Dictionary<string, SqlNode> entitiesSqlNodes,
            Dictionary<string, string> sqlWhereStatement,
            ISyntaxNode whereNode,
            string entityName,
            string currentEntityAlias,
            string wrapperEntityName,
            string clauseCondition,
            List<string> clauseType,
            Dictionary<string, List<string>> permission)
        {
            if (whereNode == null || string.IsNullOrWhiteSpace(entityName))
                return;

            foreach (var wNode in whereNode.GetNodes())
            {
                if (wrapperEntityName.Matches(entityName))
                    entityName = rootTree.Name;

                if (whereNode.ToString().TrimStart(' ').StartsWith("and:") ||
                    whereNode.ToString().TrimStart(' ').StartsWith("or:"))
                {
                    clauseCondition = whereNode.ToString()
                        .Split("{")[0]
                        .Replace(":", "")
                        .ToUpper();

                    clauseCondition = clauseCondition.Matches("AND")
                        ? "@"
                        : "$";
                }

                var wNodeChildren = wNode.GetNodes().ToList();

                if (wNodeChildren.Count < 2)
                    goto recurse;

                var nodeName = wNodeChildren[0].ToString().Trim();
                var nodeBody = wNodeChildren[1];
                var nodeBodyChildren = nodeBody.GetNodes().ToList();

                bool isCollectionOperator =
                    nodeName.Matches("some") ||
                    nodeName.Matches("all") ||
                    nodeName.Matches("none") ||
                    nodeName.Matches("any");

                if (isCollectionOperator)
                {
                    GetFieldsWhereRecursive(
                        rootTree,
                        modelsSqlNodes,
                        entitiesSqlNodes,
                        sqlWhereStatement,
                        nodeBody,
                        entityName,
                        currentEntityAlias,
                        wrapperEntityName,
                        clauseCondition,
                        clauseType,
                        permission);

                    continue;
                }

                bool isFlatField =
                    nodeBodyChildren.Count == 1 &&
                    !nodeBodyChildren[0].ToString().Contains("{") &&
                    nodeBodyChildren[0].ToString().Split(":").Length == 2;

                bool isNestedObject =
                    nodeBodyChildren.Count >= 1 &&
                    nodeBodyChildren.Any(c => c.ToString().Contains("{"));

                if (isFlatField)
                {
                    var operatorNode = nodeBodyChildren[0].ToString();
                    var operatorParts = operatorNode
                        .Split(":")
                        .Select(a => a.CleanJsonString())
                        .ToList();

                    if (operatorParts.Count < 2)
                        goto recurse;

                    var op = operatorParts[0].Trim();
                    var clauseValue = operatorParts[1].Trim();

                    if (!clauseType.Contains(op))
                        goto recurse;

                    var matchingNode = modelsSqlNodes
                        .FirstOrDefault(kvp =>
                            kvp.Key.StartsWith(
                                currentEntityAlias + "~",
                                StringComparison.OrdinalIgnoreCase) &&
                            kvp.Key.EndsWith(
                                $"~{nodeName}",
                                StringComparison.OrdinalIgnoreCase));

                    if (matchingNode.Value == null)
                    {
                        matchingNode = modelsSqlNodes
                            .FirstOrDefault(kvp =>
                                kvp.Key.EndsWith(
                                    $"~{nodeName}",
                                    StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchingNode.Value == null)
                        goto recurse;

                    var currentKeyValueNode = matchingNode.Value;

                    var enumeration = string.Empty;

                    var enumValue = currentKeyValueNode.FromEnumeration
                        .FirstOrDefault(a => a.Key.Matches(clauseValue));

                    if (!string.IsNullOrEmpty(enumValue.Key))
                    {
                        var toEnum = currentKeyValueNode.ToEnumeration
                            .FirstOrDefault(e => e.Key.Matches(enumValue.Key))
                            .Value;

                        enumeration = toEnum.ToString() ?? string.Empty;
                    }

                    var resolvedValue =
                        string.IsNullOrEmpty(enumeration)
                            ? clauseValue
                            : enumeration;

                    string condition = op switch
                    {
                        "eq" => clauseValue.Matches("null")
                            ? $" ( ~.\"{currentKeyValueNode.Column}\" IS NULL "
                            : $" ( ~.\"{currentKeyValueNode.Column}\" = '{resolvedValue}' ",

                        "neq" => clauseValue.Matches("null")
                            ? $" ( ~.\"{currentKeyValueNode.Column}\" IS NOT NULL "
                            : $" ( ~.\"{currentKeyValueNode.Column}\" <> '{resolvedValue}' ",

                        "in" => BuildInCondition(
                            clauseValue,
                            currentKeyValueNode,
                            enumeration),

                        _ => string.Empty
                    };

                    if (string.IsNullOrEmpty(condition))
                        goto recurse;

                    ApplyCondition(
                        sqlWhereStatement,
                        entitiesSqlNodes,
                        currentKeyValueNode,
                        condition,
                        currentEntityAlias);
                }
                else if (isNestedObject)
                {
                    var resolvedAlias = ResolveEntityAlias(
                        nodeName,
                        currentEntityAlias,
                        modelsSqlNodes,
                        entitiesSqlNodes);

                    GetFieldsWhereRecursive(
                        rootTree,
                        modelsSqlNodes,
                        entitiesSqlNodes,
                        sqlWhereStatement,
                        nodeBody,
                        entityName,
                        resolvedAlias,
                        wrapperEntityName,
                        clauseCondition,
                        clauseType,
                        permission);

                    continue;
                }

            recurse:
                GetFieldsWhereRecursive(
                    rootTree,
                    modelsSqlNodes,
                    entitiesSqlNodes,
                    sqlWhereStatement,
                    wNode,
                    entityName,
                    currentEntityAlias,
                    wrapperEntityName,
                    clauseCondition,
                    clauseType,
                    permission);
            }
        }

        private static void ApplyCondition(
            Dictionary<string, string> sqlWhereStatement,
            Dictionary<string, SqlNode> entitiesSqlNodes,
            SqlNode currentKeyValueNode,
            string condition,
            string currentEntityAlias)
        {
            var processedAliases =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var targetAliases =
                string.IsNullOrEmpty(currentEntityAlias)
                    ? currentKeyValueNode.LinkKeys.Select(lk => lk.AliasTo)
                    : currentKeyValueNode.LinkKeys
                        .Where(lk =>
                            lk.AliasTo.Equals(
                                currentEntityAlias,
                                StringComparison.OrdinalIgnoreCase))
                        .Select(lk => lk.AliasTo);

            if (!targetAliases.Any() &&
                !string.IsNullOrEmpty(currentEntityAlias))
            {
                var columnExistsInAlias = entitiesSqlNodes.Any(a =>
                    a.Value.Alias.Equals(
                        currentEntityAlias,
                        StringComparison.OrdinalIgnoreCase) &&
                    a.Value.Column.Equals(
                        currentKeyValueNode.Column,
                        StringComparison.OrdinalIgnoreCase));

                if (columnExistsInAlias)
                {
                    AddOrAppend(
                        sqlWhereStatement,
                        currentEntityAlias,
                        condition);
                }

                return;
            }

            foreach (var alias in targetAliases)
            {
                if (!processedAliases.Add(alias))
                    continue;

                var columnMatches = entitiesSqlNodes.Any(a =>
                    a.Value.Alias.Matches(alias) &&
                    a.Value.Column.Matches(currentKeyValueNode.Column));

                if (!columnMatches)
                    continue;

                AddOrAppend(
                    sqlWhereStatement,
                    alias,
                    condition);
            }
        }

        private static void AddOrAppend(
            Dictionary<string, string> sqlWhereStatement,
            string alias,
            string condition)
        {
            if (sqlWhereStatement.TryGetValue(alias, out var existing))
            {
                if (!existing.Contains(condition))
                    sqlWhereStatement[alias] += $" AND {condition} ) ";
            }
            else
            {
                sqlWhereStatement.Add(alias, $"{condition} ) ");
            }
        }

        private static string ResolveEntityAlias(
            string relationName,
            string currentEntityAlias,
            Dictionary<string, SqlNode> modelsSqlNodes,
            Dictionary<string, SqlNode> entitiesSqlNodes)
        {
            var knownAliases = modelsSqlNodes.Keys
                .Select(k => k.Split('~')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var match = knownAliases.FirstOrDefault(alias =>
                alias.StartsWith(
                    relationName,
                    StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(match))
                return match;

            var linkMatch = modelsSqlNodes.Values
                .SelectMany(n => n.LinkKeys)
                .FirstOrDefault(lk =>
                    lk.FromColumn.Equals(
                        relationName,
                        StringComparison.OrdinalIgnoreCase) &&
                    !lk.AliasTo.Equals(
                        currentEntityAlias,
                        StringComparison.OrdinalIgnoreCase));

            if (linkMatch != null &&
                !string.IsNullOrEmpty(linkMatch.AliasTo))
            {
                return linkMatch.AliasTo;
            }

            return relationName;
        }

        private static string BuildInCondition(
            string clauseValue,
            SqlNode node,
            string enumeration)
        {
            var inValues = string.Empty;

            var cleanedClauseValue = clauseValue
                .Trim()
                .TrimStart('[')
                .TrimEnd(']');

            foreach (var val in cleanedClauseValue.Split(','))
            {
                var valAux = val.Sanitize()
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("[", "")
                    .Replace("]", "")
                    .ToUpperCamelCase();

                inValues +=
                    $"'{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}',";
            }

            return
                $" ( ~.\"{node.Column}\" in ({inValues.TrimEnd(',')})";
        }
    }

    public class Entity
    {
        public static List<string> ClauseTypes = new()
        {
            "eq",
            "neq",
            "in",
            "any"
        };
    }
}