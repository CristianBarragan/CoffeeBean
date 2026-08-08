using System;
using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Mapping;

public static class MappingResolver
{
    public static Dictionary<Type, List<string>> BuildEntityToAliases(Dictionary<string, NodeMap> mappings)
    {
        var result = new Dictionary<Type, List<string>>();

        foreach (var kv in mappings)
        {
            if (kv.Value?.EntityType == null || string.IsNullOrWhiteSpace(kv.Key))
                continue;

            var type = kv.Value.EntityType;
            var alias = kv.Key;

            if (!result.TryGetValue(type, out var list))
            {
                list = new List<string>();
                result[type] = list;
            }

            if (!list.Contains(alias))
                list.Add(alias);
        }

        return result;
    }
}