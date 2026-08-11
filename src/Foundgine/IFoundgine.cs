using Foundgine.Execution;
using Foundgine.Semantics;

namespace Foundgine;

/// <summary>
/// Stable application-facing entry point for semantic execution.
/// </summary>
public interface IFoundgine
{
    Task<ExecutionResult> ExecuteAsync(
        SemanticRequest request,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);
}
