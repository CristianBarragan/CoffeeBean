using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// A complete, protocol-neutral semantic application model: every
/// <see cref="SemanticEntity"/> Foundgine knows how to talk about,
/// keyed by <see cref="EntityId"/>. The Milestone 2 resolver, the
/// Milestone 3 read-intent pipeline, and the Milestone 8 MCP adapter's
/// <c>discover</c> tool all read from a <see cref="SemanticModel"/>
/// instead of touching <see cref="Foundgine.Metadata"/> or
/// <see cref="Foundgine.Planning"/> directly.
///
/// Construct one with <see cref="SemanticModelBuilder"/>; there is no
/// public constructor, so a <see cref="SemanticModel"/> can never exist
/// half-built.
/// </summary>
public sealed class SemanticModel
{
    private readonly Dictionary<EntityId, SemanticEntity> _entities = new();

    internal SemanticModel()
    {
    }

    public IReadOnlyCollection<SemanticEntity> Entities => _entities.Values;

    internal void Register(SemanticEntity entity) => _entities[entity.Id] = entity;

    public bool TryGet(EntityId id, out SemanticEntity entity) =>
        _entities.TryGetValue(id, out entity!);

    public SemanticEntity Get(EntityId id) =>
        _entities.TryGetValue(id, out var entity)
            ? entity
            : throw new KeyNotFoundException(
                $"Entity {id} has no semantic descriptor registered in this SemanticModel.");
}
