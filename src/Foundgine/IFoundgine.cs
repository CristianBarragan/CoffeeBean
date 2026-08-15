using Foundgine.Execution;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine;

/// <summary>
/// Stable application-facing entry point for semantic execution.
/// </summary>
public interface IFoundgine
{
    /// <summary>
    /// Describes the domain capabilities available under the configured
    /// authorization policy. This is discovery context, not an authorization
    /// decision cache; execution evaluates the policy again.
    /// </summary>
    SemanticAuthorizationCapabilities DescribeCapabilities();

    Task<ExecutionResult> ExecuteAsync(
        SemanticRequest request,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes external, provider-neutral read intent after compiling it into
    /// the canonical semantic request. This overload is intended for adapters
    /// such as JSON APIs and AI tools.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        Foundgine.Semantics.Intent.ReadIntent intent,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);
}
