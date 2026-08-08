using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public class SqlGraphQlHelper
{
    public static List<string> ProcessFilter(NodeTree nodeTree,
        Dictionary<string, SqlNode> linkModelDictionaryTreeNode, string field, string filterType, string value,
        string filterCondition)
    {
        var enumeration = string.Empty;
        var conditions = new List<string>();

        if (string.IsNullOrEmpty(field) ||
            string.IsNullOrEmpty(filterType))
        {
            return conditions;
        }

        if (linkModelDictionaryTreeNode.TryGetValue($"{nodeTree.Name}~{field}", out var sqlNodeTo))
        {
            var enumValuePair = sqlNodeTo.FromEnumeration.FirstOrDefault(a => a.Key.Matches(value));
            
            if (enumValuePair.Key != null)
            {
                var toEnum = enumValuePair.Value.ToString();
                enumeration = toEnum;
            }
            else
            {
                enumeration = string.Empty;
            }

            switch (filterType)
            {
                case "<>":

                    if (value.Matches("null"))
                    {
                        conditions.Add($" {filterCondition} ~.\"{sqlNodeTo.Column}\" IS NOT NULL ");
                        return conditions;
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" <> '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                    return conditions;

                case "=":

                    if (value.Matches("null"))
                    {
                        conditions.Add($" {filterCondition} ~.\"{sqlNodeTo.Column}\" IS NULL ");
                        return conditions;
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" = '{(string.IsNullOrEmpty(enumeration) ? value : enumeration)}' ");
                    return conditions;

                case "in":
                    var inValues = string.Empty;
                    foreach (var val in value.Split(','))
                    {
                        var valAux = val.Sanitize().Replace("(", "").Replace(")", "").ToUpperCamelCase();
                        inValues += $"'{(string.IsNullOrEmpty(enumeration) ? valAux : enumeration)}'" + ",";
                    }

                    conditions.Add(
                        $" {filterCondition} ~.\"{sqlNodeTo.Column}\" in ({inValues.Substring(0, inValues.Length - 1)})");
                    return conditions;
            }
        }

        return conditions;
    }

    public static string HandleSort(NodeTree nodeTree, string field, string sortClause, Dictionary<string, SqlNode> linkModelDictionaryTree)
    {
        if (linkModelDictionaryTree.TryGetValue($"{nodeTree.Name}~{field}", out var sqlNodeTo))
        {
            return $" ~*~.{sqlNodeTo.RelationshipKey.Split('~')[1]} ORDER BY {sortClause},";
        }
        return string.Empty;
    }
}