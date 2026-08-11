using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Builds the static semantic topology used by resolution. This builder
/// contains only domain concepts; storage/provider metadata is deliberately
/// outside this layer.
/// </summary>
public sealed class SemanticModelBuilder
{
    private readonly SemanticModel _model = new();

    public SemanticModelBuilder Entity(
        EntityId id,
        string name,
        Action<SemanticEntityBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (_model.TryGet(id, out _))
            throw new InvalidOperationException($"Semantic entity '{id}' is already registered.");

        var builder = new SemanticEntityBuilder(id, name);
        configure(builder);
        _model.Register(builder.Build());
        return this;
    }

    public SemanticModel Build()
    {
        foreach (var entity in _model.Entities)
        {
            ValidateUniqueFields(entity);
            ValidateUniqueRelationships(entity);

            foreach (var relationship in entity.Relationships)
            {
                if (!_model.TryGet(relationship.Target, out _))
                {
                    throw new InvalidOperationException(
                        $"Semantic relationship '{entity.Name}.{relationship.Name}' targets unknown entity '{relationship.Target}'.");
                }
            }
        }

        return _model;
    }

    private static void ValidateUniqueFields(SemanticEntity entity)
    {
        if (entity.Fields.GroupBy(field => field.Id).Any(group => group.Count() > 1))
            throw new InvalidOperationException($"Semantic entity '{entity.Name}' contains duplicate field identities.");

        if (entity.Fields.GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException($"Semantic entity '{entity.Name}' contains duplicate field names.");
    }

    private static void ValidateUniqueRelationships(SemanticEntity entity)
    {
        if (entity.Relationships.GroupBy(relationship => relationship.Id).Any(group => group.Count() > 1))
            throw new InvalidOperationException($"Semantic entity '{entity.Name}' contains duplicate relationship identities.");

        if (entity.Relationships.GroupBy(relationship => relationship.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException($"Semantic entity '{entity.Name}' contains duplicate relationship names.");
    }
}
