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
        Registry[simpleKey] = map;

        return map;
    }
    
    public static IReadOnlyDictionary<string, NodeMap> GetAll()
    {
        return Registry;
    }
}