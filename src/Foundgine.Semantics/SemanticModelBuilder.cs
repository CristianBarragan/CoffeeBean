using System.Linq.Expressions;
using System.Reflection;
using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Builds the static semantic topology used by resolution. This builder
/// contains only domain concepts; storage/provider metadata is deliberately
/// outside this layer.
/// </summary>
public sealed class SemanticModelBuilder
{
    private readonly Dictionary<EntityId, SemanticEntity> _entities = new();
    private readonly Dictionary<EntityId, Type> _entityModelTypes = new();

    /// <summary>
    /// Registers a semantic entity whose fields can be authored against the
    /// application/domain model type. Property selectors inside <paramref name="configure"/>
    /// target <typeparamref name="TModel"/>, not the semantic entity builder or provider metadata.
    /// </summary>
    public SemanticModelBuilder Entity<TModel>(
        EntityId id,
        string name,
        Action<SemanticEntityBuilder<TModel>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (_entities.ContainsKey(id))
            throw new InvalidOperationException($"Semantic entity '{id}' is already registered.");

        var builder = new SemanticEntityBuilder<TModel>(id, name);
        configure(builder);
        _entities.Add(id, builder.Build());
        _entityModelTypes.Add(id, typeof(TModel));
        return this;
    }

    /// <summary>
    /// Declares a relationship with explicit domain model types and property
    /// selectors on both sides. The generic arguments are the source and target
    /// application/domain models; the selectors therefore cannot accidentally
    /// reference the wrong model, semantic metadata, or provider entity type.
    /// </summary>
    public SemanticModelBuilder Relationship<TFromModel, TToModel>(
        EntityId fromEntity,
        RelationshipId id,
        string name,
        Expression<Func<TFromModel, object?>> fromProperty,
        EntityId toEntity,
        Expression<Func<TToModel, object?>> toProperty,
        RelationshipCardinality cardinality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_entities.TryGetValue(fromEntity, out var source))
            throw new InvalidOperationException($"Source semantic entity '{fromEntity}' must be registered before declaring relationship '{name}'.");

        if (!_entities.ContainsKey(toEntity))
            throw new InvalidOperationException($"Target semantic entity '{toEntity}' must be registered before declaring relationship '{name}'.");

        if (_entityModelTypes.TryGetValue(fromEntity, out var registeredFrom) && registeredFrom != typeof(TFromModel))
            throw new ArgumentException($"Semantic entity '{source.Name}' is registered for model type '{registeredFrom.FullName}', not '{typeof(TFromModel).FullName}'.", nameof(fromEntity));

        if (_entityModelTypes.TryGetValue(toEntity, out var registeredTo) && registeredTo != typeof(TToModel))
            throw new ArgumentException($"Semantic entity '{toEntity}' is registered for model type '{registeredTo.FullName}', not '{typeof(TToModel).FullName}'.", nameof(toEntity));

        var from = GetProperty(fromProperty, typeof(TFromModel));
        var to = GetProperty(toProperty, typeof(TToModel));

        if (from.PropertyType != to.PropertyType)
        {
            throw new ArgumentException(
                $"Relationship '{source.Name}.{name}' maps '{typeof(TFromModel).Name}.{from.Name}' ({from.PropertyType.Name}) to '{typeof(TToModel).Name}.{to.Name}' ({to.PropertyType.Name}); both properties must have the same CLR type.");
        }

        var relationships = source.Relationships.ToList();
        relationships.Add(new SemanticRelationship(id, name, toEntity, cardinality));
        _entities[fromEntity] = source with { Relationships = relationships.ToArray() };
        return this;
    }

    public SemanticModelBuilder Entity(
        EntityId id,
        string name,
        Action<SemanticEntityBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (_entities.ContainsKey(id))
            throw new InvalidOperationException($"Semantic entity '{id}' is already registered.");

        var builder = new SemanticEntityBuilder(id, name);
        configure(builder);
        _entities.Add(id, builder.Build());
        return this;
    }

    private static PropertyInfo GetProperty<TModel>(Expression<Func<TModel, object?>> expression, Type modelType)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            body = conversion.Operand;

        if (body is not MemberExpression { Member: PropertyInfo property })
        {
            throw new ArgumentException(
                $"The semantic relationship property selector must be a direct property access on {modelType.Name}, such as x => x.Id.",
                nameof(expression));
        }

        if (property.DeclaringType is null || !property.DeclaringType.IsAssignableFrom(modelType))
        {
            throw new ArgumentException(
                $"Property '{property.Name}' does not belong to model type '{modelType.FullName}'.",
                nameof(expression));
        }

        return property;
    }

    public SemanticModel Build()
    {
        foreach (var entity in _entities.Values)
        {
            ValidateUniqueFields(entity);
            ValidateUniqueRelationships(entity);

            foreach (var relationship in entity.Relationships)
            {
                if (!_entities.ContainsKey(relationship.Target))
                {
                    throw new InvalidOperationException(
                        $"Semantic relationship '{entity.Name}.{relationship.Name}' targets unknown entity '{relationship.Target}'.");
                }
            }
        }

        return new SemanticModel(_entities);
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
