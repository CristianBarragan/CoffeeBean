using Foundgine.Abstractions;

namespace Foundgine.Execution.Mutation;

public sealed record MutationResult(
    int AffectedRows,
    IReadOnlyDictionary<FieldId, object?>? ReturnedValues = null);
