using Foundgine.Abstractions;
using Foundgine.Metadata;

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


    /// <summary>
    /// Discovers the structural semantic model from Foundgine.Metadata.
    /// Metadata describes what exists; this method does not grant capability
    /// exposure or authorization. Applications can enrich the result with
    /// logical traversals and policy configuration afterwards.
    /// </summary>
    public static SemanticModel Discover(IMetadataCatalog metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var entities = new Dictionary<EntityId, SemanticEntity>();
        foreach (var item in metadata.Entities)
        {
            var fields = item.EffectiveFields
                .Select(field => new SemanticField(
                    field.Id,
                    field.Name,
                    field.ClrType))
                .ToArray();

            var primary = item.PrimaryKey is null
                ? null
                : fields.FirstOrDefault(field =>
                    item.EffectiveFields.Any(source =>
                        source.Id == field.Id &&
                        source.Column?.ColumnId == item.PrimaryKey.ColumnId));

            if (primary is null)
                throw new InvalidOperationException(
                    $"Metadata entity '{item.Name}' has no field corresponding to its primary key.");

            entities[item.EntityId] = new SemanticEntity(
                item.EntityId,
                item.Name,
                new SemanticIdentity(primary.Id, primary.Name),
                fields,
                [])
            {
                ModelType = item.ClrType
            };
        }

        foreach (var relationship in metadata.Relationships)
        {
            if (!entities.TryGetValue(relationship.Source, out var source))
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' references unknown source entity '{relationship.Source}'.");

            if (!entities.ContainsKey(relationship.Target))
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' references unknown target entity '{relationship.Target}'.");

            var relationships = source.Relationships.ToList();
            relationships.Add(new SemanticRelationship(
                relationship.Id,
                relationship.Name,
                relationship.Target,
                relationship.IsCollection ? RelationshipCardinality.Many : RelationshipCardinality.One));

            entities[relationship.Source] = source with
            {
                Relationships = relationships.ToArray()
            };
        }

        return new SemanticModel(entities);
    }

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
