namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public interface IEntityMetaProvider
    {
        int Count { get; }
        
        string[][] ModelName { get; }
        string[][] Table { get; }
        string[] Schema { get; }
        string[][] ColumnName { get; }
        string[][] FieldName { get; }
        
        string[][] ConflictColumns { get; }
        CteResolutionSpec[][] CteResolutions { get; }

        bool TryGetEntityId(string modelName, out ushort entityId);
    }
}