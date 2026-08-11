using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Semantics;

/// <summary>
/// Protocol-neutral description of what a caller wants. Adapters such as
/// GraphQL translate into this shape; the engine never sees their ASTs.
/// </summary>
public sealed record SemanticRequest(
    EntityId Root,
    IReadOnlyList<SemanticSelection> Selections,
    SemanticQueryOptions? Options = null);

public sealed record SemanticSelection(
    FieldId? Field,
    RelationshipId? Relationship,
    IReadOnlyList<SemanticSelection> Children);
