namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
public sealed record EntityMetadata(EntityId Id, string Name, string TableName, IReadOnlyList<FieldMetadata> Fields);
