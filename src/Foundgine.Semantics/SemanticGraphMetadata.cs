using Foundgine.Abstractions;

namespace Foundgine.Semantics;

public sealed record SemanticTraversalOrigin(
    EntityId SourceEntity,
    IReadOnlyList<RelationshipId> Path);

public sealed record SemanticIntentOrigin(
    string Operation,
    string? Source = null);

public enum SemanticExpectedCardinality : byte
{
    Unknown,
    One,
    Many
}
