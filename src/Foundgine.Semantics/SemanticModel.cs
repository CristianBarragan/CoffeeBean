using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Immutable semantic topology produced by <see cref="SemanticModelBuilder"/>.
/// Once built, the model is safe to share across concurrent resolutions and can
/// be deterministically versioned/cached.
/// </summary>
public sealed class SemanticModel
{
    private readonly IReadOnlyDictionary<EntityId, SemanticEntity> _entities;

    internal SemanticModel(IReadOnlyDictionary<EntityId, SemanticEntity> entities)
    {
        _entities = new Dictionary<EntityId, SemanticEntity>(entities);
    }

    public IReadOnlyCollection<SemanticEntity> Entities => _entities.Values.ToArray();

    public bool TryGet(EntityId id, out SemanticEntity entity) =>
        _entities.TryGetValue(id, out entity!);

    public SemanticEntity Get(EntityId id) =>
        _entities.TryGetValue(id, out var entity)
            ? entity
            : throw new KeyNotFoundException($"Entity {id} has no semantic descriptor.");
}
