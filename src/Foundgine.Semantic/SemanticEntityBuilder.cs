using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// Fluent, hand-authoring-friendly construction of one
/// <see cref="SemanticEntity"/>. Milestone 1 is explicit that the product
/// proof must not block on a source generator ("start with hand-authored
/// metadata if necessary") -- this builder is that hand-authored path, and
/// is deliberately the kind of thing a future Roslyn generator
/// (Milestone 10) could emit calls into instead of replacing.
/// </summary>
public sealed class SemanticEntityBuilder
{
    private readonly EntityId _id;
    private readonly string _name;
    private readonly List<SemanticField> _fields = [];
    private readonly List<SemanticRelationship> _relationships = [];
    private readonly List<ActionDescriptor> _actions = [];
    private readonly List<PolicyDescriptor> _policies = [];
    private SemanticIdentity? _identity;
    private SearchCapability? _search;

    internal SemanticEntityBuilder(EntityId id, string name)
    {
        _id = id;
        _name = name;
    }

    /// <summary>Declares the field that uniquely identifies this entity. Required exactly once.</summary>
    public SemanticEntityBuilder Identity(FieldId fieldId, string name)
    {
        _identity = new SemanticIdentity(fieldId, name);
        return this;
    }

    public SemanticEntityBuilder Field(FieldId id, string name, Type clrType)
    {
        _fields.Add(new SemanticField(id, name, clrType));
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

    /// <summary>
    /// Exposes an explicit business operation on this entity. Most
    /// Milestone-1 entities call this zero times -- an empty action list
    /// is the expected, correct state until Milestone 4.
    /// </summary>
    public SemanticEntityBuilder Action(ActionDescriptor action)
    {
        _actions.Add(action);
        return this;
    }

    public SemanticEntityBuilder Policy(PolicyDescriptor policy)
    {
        _policies.Add(policy);
        return this;
    }

    /// <summary>Declares that this entity can be located by ambiguous human language.</summary>
    public SemanticEntityBuilder Search(SearchCapability capability)
    {
        _search = capability;
        return this;
    }

    internal SemanticEntity Build()
    {
        if (_identity is null)
        {
            throw new InvalidOperationException(
                $"Semantic entity '{_name}' has no identity. Every entity must declare exactly one " +
                $"identity field via .Identity(fieldId, name) -- Foundgine never infers one.");
        }

        return new SemanticEntity(
            _id,
            _name,
            _identity,
            _fields,
            _relationships,
            _actions,
            _search,
            _policies);
    }
}
