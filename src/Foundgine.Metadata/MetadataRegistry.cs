using Foundgine.Abstractions;
namespace Foundgine.Metadata;

/// <summary>
/// In-memory registry of the static metadata used by resolution and planning.
/// A future AOT generator can emit an implementation with the same contract.
/// </summary>
public sealed class MetadataRegistry : IMetadataProvider, IMutationSchema
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

    public EntityMetadata GetEntity(EntityId entityId) => Get(entityId);

    public bool TryGet(RelationshipId id, out RelationshipMetadata relationship) =>
        _relationships.TryGetValue(id, out relationship!);

    public RelationshipMetadata Get(RelationshipId id) =>
        _relationships.TryGetValue(id, out var relationship)
            ? relationship
            : throw new KeyNotFoundException($"Relationship {id} was not registered.");

    public RelationshipMetadata GetRelationship(RelationshipId relationshipId) => Get(relationshipId);
    public MutationEntitySchema GetEntitySchema(EntityId entityId)
    {
        var entity = Get(entityId);
        var fields = entity.EffectiveFields.ToDictionary(
            field => field.Id,
            field => field.Column?.ColumnId);
        var columns = entity.Columns.Select(column => column.Id).ToHashSet();
        return new MutationEntitySchema(entity.EntityId, entity.Name, columns, fields, entity.PrimaryKey?.ColumnId);
    }

    MutationEntitySchema IMutationSchema.GetEntity(EntityId entityId) => GetEntitySchema(entityId);

    public MutationRelationshipSchema GetRelationshipSchema(RelationshipId relationshipId)
    {
        var relationship = Get(relationshipId);
        return new MutationRelationshipSchema(
            relationship.Id,
            relationship.Source,
            relationship.Target,
            relationship.Name,
            relationship.SourceKey.ColumnId,
            relationship.TargetKey.ColumnId);
    }

    MutationRelationshipSchema IMutationSchema.GetRelationship(RelationshipId relationshipId) => GetRelationshipSchema(relationshipId);

}
