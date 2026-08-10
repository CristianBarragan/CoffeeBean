using Foundgine.Metadata;

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

    internal SemanticEntity Build() =>
        new(
            _id,
            _name,
            _identity ?? throw new InvalidOperationException(
                $"Semantic entity '{_name}' must declare an identity."),
            _fields,
            _relationships);
}
