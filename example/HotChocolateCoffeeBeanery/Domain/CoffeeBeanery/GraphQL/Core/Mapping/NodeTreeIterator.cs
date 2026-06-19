using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;

public static class EntityNodeTreeIterator
{
    public static EntityNodeTree GenerateTree<M>(
        Dictionary<string, EntityNodeTree> entityNodeTrees,
        M rootInstance,
        string rootAlias,
        List<KeyValuePair<string, int>> nodeIds,
        bool isModel,
        ref int counter)
        where M : class
    {
        var visitedAliases = new HashSet<string>();
        var visitedMaps = new HashSet<NodeMap>(ReferenceEqualityComparer.Instance);

        var rootMap = ResolveNodeMap(rootAlias, rootAlias);

        if (rootMap == null)
        {
            throw new InvalidOperationException(
                $"[CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTreeIterator] No NodeMap registered for root alias '{rootAlias}'. " +
                $"Registered keys: [{string.Join(", ", MappingRegistry.Registry.Keys)}]");
        }

        return IterateTree(
            entityNodeTrees,
            rootMap,
            rootAlias,
            string.Empty,
            rootAlias,
            nodeIds,
            visitedAliases,
            visitedMaps,
            ref counter)!;
    }

    private static CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree? IterateTree(
        Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree> entityNodeTrees,
        NodeMap nodeMap,
        string currentAlias,
        string parentAlias,
        string rootAlias,
        List<KeyValuePair<string, int>> nodeIds,
        HashSet<string> visitedAliases,
        HashSet<NodeMap> visitedMaps,
        ref int counter)
    {
        if (visitedAliases.Contains(currentAlias))
            return null;

        if (visitedMaps.Contains(nodeMap))
            return null;

        visitedAliases.Add(currentAlias);
        visitedMaps.Add(nodeMap);

        var id = ++counter;

        nodeIds.Add(new KeyValuePair<string, int>(currentAlias, id));

        nodeMap.Id = id;
        nodeMap.Alias = currentAlias;

        var node = new CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree
        {
            Id = id,
            Name = currentAlias,
            EntityChildren = new List<EntityKey>(),
            Mapping = nodeMap.FieldMaps,
            Schema = nodeMap.Schema
        };

        var allChildren = Enumerable.Empty<EntityKey>();

        if (nodeMap.EntityChildren != null)
        {
            allChildren = allChildren.Concat(nodeMap.EntityChildren);
        }

        if (nodeMap.EntityChildrenRelated != null)
        {
            allChildren = allChildren.Concat(nodeMap.EntityChildrenRelated);
        }

        foreach (var childLink in allChildren)
        {
            var childAlias = !string.IsNullOrWhiteSpace(childLink.AliasTo)
                ? childLink.AliasTo
                : ResolveAliasWithPrefix(childLink.To, rootAlias);

            if (string.IsNullOrWhiteSpace(childAlias))
                continue;

            if (visitedAliases.Contains(childAlias))
                continue;

            if (!MappingRegistry.Registry.TryGetValue(childAlias, out var childMap))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    $"[ERROR] NodeMap not found for child alias '{childAlias}' " +
                    $"declared in EntityChildren/EntityChildrenRelated of '{currentAlias}'. " +
                    $"Registered keys: [{string.Join(", ", MappingRegistry.Registry.Keys)}]");
                Console.ResetColor();

                continue;
            }

            if (visitedMaps.Contains(childMap))
                continue;

            var childNode = IterateTree(
                entityNodeTrees,
                childMap,
                childAlias,
                currentAlias,
                rootAlias,
                nodeIds,
                visitedAliases,
                visitedMaps,
                ref counter);

            if (childNode != null &&
                !node.EntityChildren.Any(c => c.To == childAlias))
            {
                node.EntityChildren.Add(new EntityKey
                {
                    From = currentAlias,
                    FromColumn = childLink.FromColumn,
                    To = childAlias,
                    AliasTo = childAlias,
                    ToColumn = childLink.ToColumn
                });
            }
        }

        entityNodeTrees[currentAlias] = node;

        return node;
    }

    private static string? ResolveAliasWithPrefix(
        string childAlias,
        string rootAlias)
    {
        if (MappingRegistry.Registry.ContainsKey(childAlias))
            return childAlias;

        var prefixed = $"{rootAlias}{childAlias}";

        if (MappingRegistry.Registry.ContainsKey(prefixed))
            return prefixed;

        if (childAlias.StartsWith(
                rootAlias,
                StringComparison.OrdinalIgnoreCase))
        {
            var stripped = childAlias.Substring(rootAlias.Length);

            var strippedPrefixed = $"{rootAlias}{stripped}";

            if (MappingRegistry.Registry.ContainsKey(strippedPrefixed))
                return strippedPrefixed;
        }

        var candidates = MappingRegistry.Registry.Keys
            .Where(k => k.EndsWith(
                childAlias,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidates.FirstOrDefault(k =>
                   k.StartsWith(
                       rootAlias,
                       StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault();
    }

    private static NodeMap? ResolveNodeMap(
        string alias,
        string rootAlias)
    {
        if (MappingRegistry.Registry.TryGetValue(alias, out var map))
            return map;

        var resolved = ResolveAliasWithPrefix(alias, rootAlias);

        return resolved != null &&
               MappingRegistry.Registry.TryGetValue(
                   resolved,
                   out var resolvedMap)
            ? resolvedMap
            : null;
    }
}