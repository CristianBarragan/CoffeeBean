using CoffeeBeanery.GraphQL.Core.Helper;
using CoffeeBeanery.GraphQL.Core.Mapping;

namespace CoffeeBeanery.GraphQL.Core.Warmup;

public static class MappingWarmup
{
    public static void Warmup(IReadOnlyDictionary<string, NodeMap> mapping)
    {
        var entityNodeTrees = new Dictionary<string, CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree>();
        var nodeIds = new List<KeyValuePair<string, int>>();
        var counter = 0;

        foreach (var (_, map) in mapping)
        {
            if (map.ModelType == null || map.EntityType == null)
                continue;

            WarmupMap(map);
            BulkMapper.Compile(map);
        }

        foreach (var (registeredKey, map) in mapping)
        {
            if (map.ModelType == null || map.EntityType == null)
                continue;
            
            var isChild = mapping.Values.Any(parent =>
                parent.EntityChildren.Any(c =>
                    string.Equals(
                        c.To,
                        map.EntityType.Name,
                        StringComparison.OrdinalIgnoreCase))
                ||
                parent.EntityChildrenRelated.Any(c =>
                    string.Equals(
                        c.To,
                        map.EntityType.Name,
                        StringComparison.OrdinalIgnoreCase)));

            if (isChild)
                continue;

            var rootAlias = registeredKey;

            map.Alias = registeredKey;

            if (entityNodeTrees.ContainsKey(rootAlias))
                continue;

            object? modelInstance;

            try
            {
                modelInstance = Activator.CreateInstance(map.ModelType);
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"[WARNING] Could not instantiate {map.ModelType.FullName}");
                Console.ResetColor();

                continue;
            }

            try
            {
                EntityNodeTreeIterator.GenerateTree(
                    entityNodeTrees,
                    modelInstance,
                    rootAlias,
                    nodeIds,
                    isModel: true,
                    ref counter);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"[WARNING] GenerateTree failed for alias '{rootAlias}': {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static void WarmupMap(NodeMap map)
    {
        foreach (var field in map.FieldMaps.Where(f => f.DestinationName != "Id"))
        {
            var modelProp =
                map.ModelType?.GetProperty(field.SourceName);

            var entityProp =
                map.EntityType?.GetProperty(field.DestinationName);

            if (modelProp != null)
                map.ModelProperties[field.SourceName] = modelProp;

            if (entityProp != null)
                map.EntityProperties[field.DestinationName] = entityProp;
        }
    }
}