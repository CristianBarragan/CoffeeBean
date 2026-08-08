namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public interface IEntityMetaProvider
    {
        // ---- Model-keyed (indexed by EntityId.*) ----
        int Count { get; }
        string[][] ModelName { get; }
        ushort[][] FieldToColumn { get; }
        FieldMapSpec[][] FieldMappings { get; }
        string[][] Table { get; }
        string[][] Schema { get; }
        string[][] ColumnName { get; }
        string[][] FieldName { get; }
        ConflictColumn[][] EntityConflictColumns { get; }
        CteResolutionSpec[][] CteResolutions { get; }

        // ---- Storage-entity-keyed (indexed by StorageEntityId.*) ----
        int StorageEntityCount { get; }
        string[] EntitySchema { get; }           // [storageEntityId]
        string[] EntityTable { get; }            // [storageEntityId]
        string[][] EntityColumnName { get; }     // [storageEntityId][columnId]

        bool TryGetEntityId(string modelName, out ushort entityId);
    }
}