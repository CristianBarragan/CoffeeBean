namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public interface IEntityMetaProvider
    {
        // ---- Model-keyed (indexed by EntityId.*) ----
        int Count { get; }
        string[][] ModelName { get; }
        ushort[][] FieldToColumn { get; }
        FieldMapSpec[][] FieldMappings { get; }
        string[][] Table { get; }          // legacy — [modelId][0]; use EntityTable for SQL generation
        string[] Schema { get; }           // legacy — [modelId]; use EntitySchema for SQL generation
        string[][] ColumnName { get; }     // legacy — [modelId][columnId]; use EntityColumnName
        string[][] FieldName { get; }      // [modelId][fieldId] camelCase GraphQL field names
        string[][] ConflictColumns { get; }
        CteResolutionSpec[][] CteResolutions { get; }

        // ---- Storage-entity-keyed (indexed by StorageEntityId.*) ----
        int StorageEntityCount { get; }
        string[] EntitySchema { get; }           // [storageEntityId]
        string[] EntityTable { get; }            // [storageEntityId]
        string[][] EntityColumnName { get; }     // [storageEntityId][columnId]

        bool TryGetEntityId(string modelName, out ushort entityId);
    }
}