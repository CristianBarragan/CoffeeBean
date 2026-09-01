using Foundgine.Abstractions;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// Common provider-neutral mutation input used when composing dependent mutation batches.
/// </summary>
public interface IMutationIntent
{
    EntityId Entity { get; }
    MutationKind Kind { get; }
    IReadOnlyList<MutationFieldValue> Fields { get; }
}
