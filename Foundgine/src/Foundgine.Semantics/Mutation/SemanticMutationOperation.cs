using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Semantics.Mutation;

/// <summary>
/// Canonical semantic representation of one mutation operation.
/// It contains semantic identities and intent only; physical columns, SQL,
/// provider plans and transaction mechanics are intentionally absent.
/// </summary>
public sealed record SemanticMutationOperation(
    EntityId Entity,
    SemanticMutationKind Kind,
    IReadOnlyList<SemanticMutationField> Fields,
    SemanticFilterExpression? Filter,
    IReadOnlyList<FieldId> ConflictFields,
    IReadOnlyList<FieldId> ReturnFields,
    IReadOnlyList<SemanticMutationEffect> Effects,
    IReadOnlyList<SemanticMutationDependency> Dependencies);
