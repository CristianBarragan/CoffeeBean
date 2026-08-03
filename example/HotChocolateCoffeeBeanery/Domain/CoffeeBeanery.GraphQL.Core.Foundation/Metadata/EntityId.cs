namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
public readonly record struct EntityId(Guid Value) { public static EntityId New() => new(Guid.NewGuid()); }
