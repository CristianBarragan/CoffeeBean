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
    private readonly IReadOnlyList<SemanticTraversal> _traversals;

    internal SemanticModel(IReadOnlyDictionary<EntityId, SemanticEntity> entities, IReadOnlyList<SemanticTraversal>? traversals = null)
    {
        _entities = new Dictionary<EntityId, SemanticEntity>(entities);
        _traversals = traversals?.ToArray() ?? [];
    }

    public IReadOnlyCollection<SemanticEntity> Entities => _entities.Values.ToArray();

    /// <summary>Logical open-intent traversals. These may span multiple relationships.</summary>
    public IReadOnlyList<SemanticTraversal> Traversals => _traversals;


    public bool TryGetTraversal(EntityId source, string name, out SemanticTraversal traversal)
    {
        traversal = _traversals.FirstOrDefault(x =>
            x.Source == source && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))!;
        return traversal is not null;
    }

    public SemanticTraversal GetTraversal(EntityId source, string name) =>
        TryGetTraversal(source, name, out var traversal)
            ? traversal
            : throw new KeyNotFoundException($"Semantic traversal '{name}' is not defined on entity '{Get(source).Name}'.");

    public bool TryGet(EntityId id, out SemanticEntity entity) =>
        _entities.TryGetValue(id, out entity!);

    public SemanticEntity Get(EntityId id) =>
        _entities.TryGetValue(id, out var entity)
            ? entity
            : throw new KeyNotFoundException($"Entity {id} has no semantic descriptor.");
}
