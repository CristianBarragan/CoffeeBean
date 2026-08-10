namespace Foundgine.Metadata;
public sealed record RelationshipMetadata(RelationshipId Id, EntityId Source, EntityId Target, string Name);
