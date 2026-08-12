using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Planning.Mutation;

public sealed record MutationOperation(
    MutationEntitySchema Entity,
    MutationKind Kind,
    IReadOnlyList<MutationFieldValue> Fields,
    SemanticFilterExpression? Filter,
    IReadOnlyList<ColumnId>? ConflictColumns = null,
    IReadOnlyList<FieldId>? ReturnFields = null);
