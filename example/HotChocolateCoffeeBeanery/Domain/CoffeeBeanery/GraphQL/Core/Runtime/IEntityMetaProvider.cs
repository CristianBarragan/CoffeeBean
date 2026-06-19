namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    /// <summary>
    /// Abstracts access to the source-generated EntityMeta and EntityId arrays,
    /// allowing CoffeeBeanery.GraphQL.Core to depend on metadata without a
    /// circular reference to the project that owns the generated types.
    /// Implemented by GeneratedEntityMetaProvider in Domain.Shared (auto-generated).
    /// </summary>
    public interface IEntityMetaProvider
    {
        int Count { get; }
        string[] Table { get; }
        string[] Schema { get; }
        string[][] ColumnName { get; }
        string[][] FieldName { get; }

        bool TryGetEntityId(string modelName, out ushort entityId);
    }
}