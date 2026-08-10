namespace Foundgine.Metadata;

/// <summary>
/// In-memory registry of the static metadata used by resolution and planning.
/// A future AOT generator can emit an implementation with the same contract.
/// </summary>
public sealed class MetadataRegistry
{
    private readonly Dictionary<EntityId, EntityMetadata> _entities = new();
    private readonly Dictionary<RelationshipId, RelationshipMetadata> _relationships = new();

    public IEnumerable<EntityMetadata> Entities => _entities.Values;
    public IEnumerable<RelationshipMetadata> Relationships => _relationships.Values;

    public void Register(EntityMetadata metadata) =>
        _entities[metadata.EntityId] = metadata;

    public void Register(RelationshipMetadata relationship) =>
        _relationships[relationship.Id] = relationship;

    public bool TryGet(EntityId id, out EntityMetadata metadata) =>
        _entities.TryGetValue(id, out metadata!);

    public EntityMetadata Get(EntityId id) =>
        _entities.TryGetValue(id, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Entity {id} was not registered.");

    public bool TryGet(RelationshipId id, out RelationshipMetadata relationship) =>
        _relationships.TryGetValue(id, out relationship!);

    public RelationshipMetadata Get(RelationshipId id) =>
        _relationships.TryGetValue(id, out var relationship)
            ? relationship
            : throw new KeyNotFoundException($"Relationship {id} was not registered.");
}
