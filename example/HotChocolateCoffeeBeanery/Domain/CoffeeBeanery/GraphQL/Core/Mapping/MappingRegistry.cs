using CoffeeBeanery.GraphQL.Core.Mapping;

public static class MappingRegistry
{
    public static Dictionary<string, NodeMap> Registry { get; } = new();
    private static int _idCounter = 0;

    public static NodeMap Register(
        Type modelType,
        Type? entityType,
        NodeMap map,
        string alias)
    {
        map.ModelType  = modelType;
        map.EntityType = entityType;

        var simpleKey = alias;

       if (Registry.TryGetValue(simpleKey, out var existing) && existing != map)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(
                $"[ERROR] MappingRegistry key collision on '{simpleKey}': a mapping for " +
                $"ModelType '{existing.ModelType?.Name}' / EntityType '{existing.EntityType?.Name}' " +
                $"already exists. The new registration for ModelType '{modelType.Name}' / " +
                $"EntityType '{entityType?.Name}' is being DISCARDED (keeping the original) - " +
                $"give the duplicate registration a distinct alias to disambiguate, or find " +
                $"and remove the call site causing the duplicate Register() call.");
            Console.ResetColor();

            return existing;
        }

        Registry[simpleKey] = map;

        return map;
    }

    public static IReadOnlyDictionary<string, NodeMap> GetAll()
    {
        return Registry;
    }
}