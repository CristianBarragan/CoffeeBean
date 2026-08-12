using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// A named semantic relationship between two entities.
/// </summary>
public sealed record SemanticRelationship(
    RelationshipId Id,
    string Name,
    EntityId Target,
    RelationshipCardinality Cardinality);
