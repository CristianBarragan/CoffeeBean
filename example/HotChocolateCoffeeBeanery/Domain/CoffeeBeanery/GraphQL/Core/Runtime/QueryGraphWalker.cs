using CoffeeBeanery.GraphQL.Core.Sql;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class QueryGraphWalker
{
    public sealed class Result
    {
        public Dictionary<string, List<string>> ColumnsByRowKey { get; } = new();
        public Dictionary<string, string> AliasByRowKey { get; } = new();
        public List<string> RowOrder { get; } = new();

        public void Add(string rowKey, string alias, string column)
        {
            if (!ColumnsByRowKey.TryGetValue(rowKey, out var list))
            {
                list = new();
                ColumnsByRowKey[rowKey] = list;
                AliasByRowKey[rowKey] = alias;
                RowOrder.Add(rowKey);
            }

            if (!list.Contains(column))
                list.Add(column);
        }
    }

    public static Result Walk(string rootAlias, SelectionSetNode? set)
    {
        var result = new Result();
        result.Add(rootAlias, rootAlias, "Id");

        if (set != null)
            WalkSet(rootAlias, rootAlias, set, result);

        return result;
    }

    static void WalkSet(string alias, string row, SelectionSetNode set, Result r)
    {
        foreach (var s in set.Selections)
        {
            if (s is not FieldNode f) continue;

            var name = f.Name.Value;

            if (NodeRegistry.FrozenChildAliasByField.TryGetValue((alias, name), out var child))
            {
                var childRow = row + "." + name;

                if (NodeRegistry.FrozenEntityTrees.ContainsKey(child))
                    r.Add(childRow, child, "Id");

                if (f.SelectionSet != null)
                    WalkSet(child, childRow, f.SelectionSet, r);

                continue;
            }

            foreach (var (_, col) in NodeRegistry.ResolveLeaf(alias, name))
                r.Add(row, alias, col);

            if (f.SelectionSet != null)
                WalkSet(alias, row, f.SelectionSet, r);
        }
    }
}