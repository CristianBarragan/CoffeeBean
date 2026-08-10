namespace Foundgine.Metadata;

/// <summary>
/// Runtime access to generated/static metadata. The core depends on this
/// abstraction rather than on a particular generator implementation.
/// </summary>
public interface IMetadataProvider
{
    EntityMetadata GetEntity(EntityId entityId);
    RelationshipMetadata GetRelationship(RelationshipId relationshipId);
}
