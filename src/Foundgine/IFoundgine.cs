using Foundgine.Execution;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Security.Execution;
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

    /// <summary>Returns the canonical machine-readable semantic capability contract.</summary>
    SemanticCapabilityContract DescribeCapabilityContract();

    /// <summary>
    /// Returns the capability contract visible to a verified warrant-backed caller.
    /// Discovery never consumes replay state; execution still re-authorizes.
    /// </summary>
    SemanticCapabilityContract DescribeCapabilityContract(SecurityExecutionContext security);

    /// <summary>Returns the semantic compatibility versions used by this engine.</summary>
    SemanticVersionSet DescribeVersionSet();

    /// <summary>Plans and authorizes a request without executing provider work.</summary>
    DryRunResult DryRun(SemanticRequest request);

    /// <summary>Creates an approval bound to the exact currently authorized plan.</summary>
    PlanApproval ApprovePlan(SemanticRequest request, string approvedBy);

    /// <summary>Executes only when the current authorized plan exactly matches the approval fingerprint.</summary>
    Task<ExecutionResult> ExecuteApprovedAsync(
        PlanApproval approval,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);

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
