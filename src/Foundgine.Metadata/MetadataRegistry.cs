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
    private readonly Dictionary<ModelId, ModelMetadata> _models = new();
    private readonly Dictionary<ConnectionId, ConnectionMetadata> _connections = new();
    private readonly List<ConversionMetadata> _conversions = new();

    public IEnumerable<EntityMetadata> Entities => _entities.Values;
    public IEnumerable<RelationshipMetadata> Relationships => _relationships.Values;
    public IEnumerable<ModelMetadata> Models => _models.Values;
    public IEnumerable<ConnectionMetadata> Connections => _connections.Values;
    public IEnumerable<ConversionMetadata> Conversions => _conversions;

    public void Register(EntityMetadata metadata) =>
        _entities[metadata.EntityId] = metadata;

    public void Register(RelationshipMetadata relationship) =>
        _relationships[relationship.Id] = relationship;

    public void Register(ModelMetadata model) =>
        _models[model.Id] = model;

    public void Register(ConnectionMetadata connection) =>
        _connections[connection.Id] = connection;

    public void Register(ConversionMetadata conversion)
    {
        if (_conversions.Any(x => x.SourceType == conversion.SourceType && x.TargetType == conversion.TargetType))
            throw new InvalidOperationException($"Duplicate Foundgine conversion {conversion.SourceType} -> {conversion.TargetType}.");
        _conversions.Add(conversion);
    }

    public bool TryGet(EntityId id, out EntityMetadata metadata) =>
        _entities.TryGetValue(id, out metadata!);

    public EntityMetadata Get(EntityId id) =>
        _entities.TryGetValue(id, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Entity {id} was not registered.");

    public EntityMetadata GetEntity(EntityId entityId) => Get(entityId);

    public bool TryGet(ModelId id, out ModelMetadata model) =>
        _models.TryGetValue(id, out model!);

    public ModelMetadata GetModel(ModelId id) => Get(id);

    public ModelMetadata Get(ModelId id) =>
        _models.TryGetValue(id, out var model)
            ? model
            : throw new KeyNotFoundException($"Model {id} was not registered.");

    public bool TryGet(ConnectionId id, out ConnectionMetadata connection) =>
        _connections.TryGetValue(id, out connection!);

    public ConnectionMetadata GetConnection(ConnectionId id) => Get(id);

    public ConversionMetadata? FindConversion(Type sourceType, Type targetType) =>
        _conversions.FirstOrDefault(x => x.SourceType == sourceType && x.TargetType == targetType);

    public ConnectionMetadata Get(ConnectionId id) =>
        _connections.TryGetValue(id, out var connection)
            ? connection
            : throw new KeyNotFoundException($"Connection {id} was not registered.");

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
