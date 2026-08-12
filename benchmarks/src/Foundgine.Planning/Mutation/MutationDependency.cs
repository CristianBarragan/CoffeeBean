using Foundgine.Abstractions;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// A directed dependency between two mutation operations.
/// </summary>
public sealed record MutationDependency(
    int SourceOperationIndex,
    int TargetOperationIndex,
    FieldId SourceField,
    ColumnId TargetColumn);
