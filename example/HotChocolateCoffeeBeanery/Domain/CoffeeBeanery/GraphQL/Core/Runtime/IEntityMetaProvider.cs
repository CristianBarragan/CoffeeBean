namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    /// <summary>
    /// Abstracts access to source-generated metadata arrays.
    /// Two index spaces:
    ///
    ///   EntityId / ModelId  (0..EntityId.Count-1)
    ///     → Schema[], ModelName[][], Table[][], FieldName[][], ConflictColumns[][], CteResolutions[][]
    ///     → One entry per registered MODEL (the GraphQL schema side)
    ///
    ///   StorageEntityId  (0..StorageEntityId.Count-1)
    ///     → EntitySchema[], EntityTable[], EntityColumnName[][]
    ///     → One entry per unique DB ENTITY TYPE referenced across all mappings
    ///     → ColumnId.{EntityName}.* constants index into EntityColumnName[StorageEntityId.{EntityName}]
    /// </summary>
    public interface IEntityMetaProvider
    {
        // ---- Model-keyed (indexed by EntityId.*) ----
        int Count { get; }
        string[][] ModelName { get; }
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