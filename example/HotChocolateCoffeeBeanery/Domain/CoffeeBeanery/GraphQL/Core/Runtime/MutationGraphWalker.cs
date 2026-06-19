using CoffeeBeanery.GraphQL.Core.Sql;
using HotChocolate.Language;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class MutationGraphWalker
{
    public sealed class Result
    {
        public Dictionary<string, List<(string Column, string Value)>> ColumnsByAlias { get; } = new();
        public List<string> AliasOrder { get; } = new();

        public void Add(string alias, string column, string value)
        {
            if (!ColumnsByAlias.TryGetValue(alias, out var list))
            {
                list = new();
                ColumnsByAlias[alias] = list;
                AliasOrder.Add(alias);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Column == column)
                {
                    list[i] = (column, value);
                    return;
                }
            }

            list.Add((column, value));
        }
    }

    public static Result Walk(string rootAlias, IValueNode node)
    {
        var result = new Result();
        WalkNode(rootAlias, node, result);
        return result;
    }

    static void WalkNode(string alias, IValueNode node, Result result)
    {
        if (node is ListValueNode list)
        {
            foreach (var i in list.Items)
                WalkNode(alias, i, result);
            return;
        }

        if (node is not ObjectValueNode obj)
            return;

        foreach (var f in obj.Fields)
        {
            var name = f.Name.Value;

            if (f.Value is ObjectValueNode or ListValueNode)
            {
                if (NodeRegistry.FrozenChildAliasByField.TryGetValue((alias, name), out var child))
                    WalkNode(child, f.Value, result);

                continue;
            }

            var raw = f.Value.Value?.ToString();
            if (raw is null) continue;

            foreach (var (ea, col) in NodeRegistry.ResolveLeaf(alias, name))
                result.Add(ea, col, raw);
        }
    }
}