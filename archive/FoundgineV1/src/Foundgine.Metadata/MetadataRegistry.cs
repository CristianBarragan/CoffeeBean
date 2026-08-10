namespace Foundgine.Metadata;
public sealed class MetadataRegistry
{
 private readonly Dictionary<EntityId, EntityMetadata> _entities = new();
 public void Register(EntityMetadata metadata) => _entities[metadata.EntityId]=metadata;
 public bool TryGet(EntityId id, out EntityMetadata metadata)=>_entities.TryGetValue(id,out metadata!);
 public EntityMetadata Get(EntityId id)=>_entities.TryGetValue(id,out var m)?m:throw new KeyNotFoundException($"Entity {id} was not registered.");
}
