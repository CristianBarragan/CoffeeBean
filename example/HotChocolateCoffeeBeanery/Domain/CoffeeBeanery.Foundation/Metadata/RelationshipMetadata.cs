namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
public sealed record RelationshipMetadata(RelationshipId Id, EntityId Source, EntityId Target, string Name);
