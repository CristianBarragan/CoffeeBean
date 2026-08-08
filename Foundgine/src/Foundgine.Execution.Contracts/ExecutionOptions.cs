namespace Foundgine.Execution.Contracts;
public sealed record ExecutionOptions(bool EnableDiagnostics = false, int MaxDepth = 64);
