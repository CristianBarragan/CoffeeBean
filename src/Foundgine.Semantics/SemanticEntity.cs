using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// The protocol-independent semantic description of an entity.
/// </summary>
public sealed record SemanticEntity(
    EntityId Id,
    string Name,
    SemanticIdentity Identity,
    IReadOnlyList<SemanticField> Fields,
    IReadOnlyList<SemanticRelationship> Relationships)
{
    /// <summary>CLR model type represented by this semantic entity when known.</summary>
    public Type? ModelType { get; init; }
}
