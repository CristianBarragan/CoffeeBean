using System.Linq.Expressions;
using System.Reflection;
using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Small hand-authored construction path. AOT generation can target these
/// same semantic shapes later.
/// </summary>
public sealed class SemanticEntityBuilder
{
    private readonly EntityId _id;
    private readonly string _name;
    private readonly List<SemanticField> _fields = [];
    private readonly List<SemanticRelationship> _relationships = [];
    private SemanticIdentity? _identity;

    internal SemanticEntityBuilder(EntityId id, string name)
    {
        _id = id;
        _name = name;
    }

    public SemanticEntityBuilder Identity(FieldId fieldId, string name)
    {
        _identity = new SemanticIdentity(fieldId, name);
        return this;
    }

    public SemanticEntityBuilder Field(
        FieldId id,
        string name,
        Type clrType,
        SemanticType? semanticType = null,
        SemanticFieldCapabilities capabilities = SemanticFieldCapabilities.Default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clrType);
        _fields.Add(new SemanticField(id, name, clrType, semanticType, capabilities));
        return this;
    }

    public SemanticEntityBuilder Relationship(
        RelationshipId id,
        string name,
        EntityId target,
        RelationshipCardinality cardinality)
    {
        _relationships.Add(new SemanticRelationship(id, name, target, cardinality));
        return this;
    }

    internal SemanticEntity Build() =>
        new(
            _id,
            _name,
            _identity ?? throw new InvalidOperationException(
                $"Semantic entity '{_name}' must declare an identity."),
            _fields.ToArray(),
            _relationships.ToArray());
}

/// <summary>
/// Strongly typed manual semantic builder. Property selectors target the
/// application/domain model type <typeparamref name="TModel"/>; they do not
/// target Foundgine's semantic entity metadata or a provider's entity type.
/// </summary>
/// <typeparam name="TModel">The application/domain model represented by the semantic entity.</typeparam>
public sealed class SemanticEntityBuilder<TModel>
{
    private readonly EntityId _id;
    private readonly string _name;
    private readonly List<SemanticField> _fields = [];
    private readonly List<SemanticRelationship> _relationships = [];
    private SemanticIdentity? _identity;
    // Reserve field identity 1 for the semantic identity. This keeps the
    // generated IDs stable even if fields are authored before Identity(...).
    private ushort _nextFieldId = 2;

    internal SemanticEntityBuilder(EntityId id, string name)
    {
        _id = id;
        _name = name;
    }

    /// <summary>
    /// Declares the semantic identity using a property on <typeparamref name="TModel"/>.
    /// Foundgine reserves the entity-local identity field as <c>FieldId(1)</c>;
    /// callers do not need to construct a <see cref="FieldId"/>.
    /// </summary>
    public SemanticEntityBuilder<TModel> Identity<TProperty>(Expression<Func<TModel, TProperty>> property, string? semanticName = null)
    {
        var metadata = GetProperty(property);
        _identity = new SemanticIdentity(new FieldId(1), semanticName ?? metadata.Name);
        return this;
    }

    /// <summary>
    /// Exposes a property from <typeparamref name="TModel"/> as a semantic field.
    /// The CLR type, field name, and entity-local field identity are derived from
    /// the property selector.
    /// </summary>
    public SemanticEntityBuilder<TModel> Field<TProperty>(
        Expression<Func<TModel, TProperty>> property,
        SemanticType? semanticType = null,
        SemanticFieldCapabilities capabilities = SemanticFieldCapabilities.Default)
    {
        var metadata = GetProperty(property);
        _fields.Add(new SemanticField(
            AllocateFieldId(),
            metadata.Name,
            metadata.PropertyType,
            semanticType,
            capabilities));
        return this;
    }

    /// <summary>
    /// Declares a relationship with strongly typed property selectors on both
    /// sides. <typeparamref name="TModel"/> is the source/domain model and
    /// <typeparamref name="TTargetModel"/> is the target/domain model. The
    /// selected properties are validated during semantic-model construction;
    /// they are not semantic-entity or provider metadata properties.
    /// </summary>
    public SemanticEntityBuilder<TModel> Relationship<TTargetModel>(
        RelationshipId id,
        string name,
        Expression<Func<TModel, object?>> fromProperty,
        Expression<Func<TTargetModel, object?>> toProperty,
        EntityId target,
        RelationshipCardinality cardinality)
    {
        var from = GetProperty(fromProperty);
        var to = GetProperty<TTargetModel>(toProperty);

        if (from.PropertyType != to.PropertyType)
        {
            throw new ArgumentException(
                $"Relationship '{_name}.{name}' maps '{typeof(TModel).Name}.{from.Name}' ({from.PropertyType.Name}) to '{typeof(TTargetModel).Name}.{to.Name}' ({to.PropertyType.Name}); both properties must have the same CLR type.");
        }

        _relationships.Add(new SemanticRelationship(id, name, target, cardinality));
        return this;
    }

    public SemanticEntityBuilder<TModel> Relationship(
        RelationshipId id,
        string name,
        EntityId target,
        RelationshipCardinality cardinality)
    {
        _relationships.Add(new SemanticRelationship(id, name, target, cardinality));
        return this;
    }

    internal SemanticEntity Build() =>
        new(
            _id,
            _name,
            _identity ?? throw new InvalidOperationException(
                $"Semantic entity '{_name}' must declare an identity."),
            _fields.ToArray(),
            _relationships.ToArray())
        { ModelType = typeof(TModel) };

    private FieldId AllocateFieldId()
    {
        if (_nextFieldId == ushort.MaxValue)
            throw new InvalidOperationException($"Semantic entity '{_name}' has too many fields.");

        return new FieldId(_nextFieldId++);
    }

    private static PropertyInfo GetProperty<TTargetModel>(Expression<Func<TTargetModel, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            body = conversion.Operand;

        if (body is not MemberExpression { Member: PropertyInfo property })
        {
            throw new ArgumentException(
                $"The semantic relationship property selector must be a direct property access on {typeof(TTargetModel).Name}, such as x => x.Id.",
                nameof(expression));
        }

        if (property.DeclaringType is null || !property.DeclaringType.IsAssignableFrom(typeof(TTargetModel)))
        {
            throw new ArgumentException(
                $"Property '{property.Name}' does not belong to model type '{typeof(TTargetModel).FullName}'.",
                nameof(expression));
        }

        return property;
    }

    private static PropertyInfo GetProperty<TProperty>(Expression<Func<TModel, TProperty>> expression) =>
        GetProperty(expression, nameof(expression));

    private static PropertyInfo GetProperty<TProperty>(Expression<Func<TModel, TProperty>> expression, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            body = conversion.Operand;

        if (body is not MemberExpression { Member: PropertyInfo property })
        {
            throw new ArgumentException(
                $"The semantic property selector must be a direct property access on {typeof(TModel).Name}, such as x => x.Id.",
                nameof(expression));
        }

        if (property.DeclaringType is null || !property.DeclaringType.IsAssignableFrom(typeof(TModel)))
        {
            throw new ArgumentException(
                $"Property '{property.Name}' does not belong to model type '{typeof(TModel).FullName}'.",
                nameof(expression));
        }

        return property;
    }
}
