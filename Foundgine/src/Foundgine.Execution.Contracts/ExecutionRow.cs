namespace Foundgine.Execution.Contracts;

/// <summary>
/// A single streamed row produced by an execution provider: raw column
/// values per entity, keyed by entity id.
/// </summary>
public sealed record ExecutionRow(
    IReadOnlyDictionary<ushort, object?[]> Entities
);
