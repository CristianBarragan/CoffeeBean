using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// Provider-neutral description of one entity mutation.
/// Update and Delete require a filter; Create does not.
/// </summary>
public sealed record MutationIntent(
    EntityId Entity,
    MutationKind Kind,
    IReadOnlyList<MutationFieldValue> Fields,
    SemanticFilterExpression? Filter = null,
    IReadOnlyList<FieldId>? ReturnFields = null) : IMutationIntent;
