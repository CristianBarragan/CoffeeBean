namespace Foundgine.Execution.Contracts;
public sealed record ExecutionContext(Guid ExecutionId, IReadOnlyDictionary<string, object?> Variables);
