using Foundgine.Abstractions;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// References a value returned by an earlier mutation operation in the same batch.
/// The reference is resolved by the execution provider after the source operation commits
/// within the current transaction.
/// </summary>
public sealed record MutationValueReference(
    int SourceOperationIndex,
    FieldId SourceField);
