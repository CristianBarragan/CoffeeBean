namespace Foundgine.Execution.Contracts;
public sealed record ExecutionResult(bool Success, object? Data, IReadOnlyList<string> Errors);
