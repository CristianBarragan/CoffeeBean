using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Static semantic model derived from domain metadata. This is the source
/// topology from which request graphs are resolved.
/// </summary>
public sealed class SemanticModel
{
    private readonly Dictionary<EntityId, SemanticEntity> _entities = new();

    internal SemanticModel() { }

    public IReadOnlyCollection<SemanticEntity> Entities => _entities.Values;

    internal void Register(SemanticEntity entity) => _entities[entity.Id] = entity;

    public bool TryGet(EntityId id, out SemanticEntity entity) =>
        _entities.TryGetValue(id, out entity!);

    public SemanticEntity Get(EntityId id) =>
        _entities.TryGetValue(id, out var entity)
            ? entity
            : throw new KeyNotFoundException($"Entity {id} has no semantic descriptor.");
}
