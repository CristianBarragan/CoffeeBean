namespace Foundgine.Execution;

public sealed record ExecutionResult(
    IReadOnlyList<ExecutionRow> Rows);

public sealed record ExecutionRow(
    IReadOnlyDictionary<string, object?> Values);
