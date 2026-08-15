using Foundgine.Execution;
using Foundgine.Execution.Mutation;
using Foundgine.Semantics.Mutation;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine;

public interface IFoundgineMutations
{
    MutationDryRunResult DryRun(SemanticMutationRequest request);
    MutationPlanApproval Approve(SemanticMutationRequest request, string approvedBy);
    Task<MutationExecutionResult> ExecuteApprovedAsync(
        MutationPlanApproval approval,
        ExecutionContext? context = null,
        CancellationToken cancellationToken = default);
}

public sealed record SemanticMutationRequest(SemanticMutationOperationGraph Graph);

public sealed record MutationDryRunResult(
    string PlanFingerprint,
    IReadOnlyList<MutationPlanOperation> Operations,
    IReadOnlyList<string> Effects);

public sealed record MutationPlanOperation(
    int Index,
    string Entity,
    string Kind,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> ReturnFields);

public sealed record MutationPlanApproval(
    SemanticMutationRequest Request,
    string ApprovalId,
    string PlanFingerprint,
    string ApprovedBy,
    DateTimeOffset ApprovedAt);

public sealed record MutationExecutionResult(
    MutationBatchResult Result,
    string PlanFingerprint,
    string ResultFingerprint,
    string? ApprovalId,
    string? ApprovedBy);
