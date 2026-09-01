using Foundgine;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Security.Execution;
using Foundgine.Semantics.Security.Warrants;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.Testing;

/// <summary>
/// Explicit MCP boundary double. It implements only capability discovery because
/// these tests are about host security-context plumbing, not execution.
/// Any accidental execution call fails with a diagnostic message rather than
/// silently behaving like an unfinished generic stub.
/// </summary>
public sealed class CapabilityOnlyFoundgine : Foundgine.IFoundgine
{
    public SemanticAuthorizationCapabilities DescribeCapabilities() =>
        throw NotUsed(nameof(DescribeCapabilities));

    public SemanticCapabilityContract DescribeCapabilityContract() => Contract();

    public SemanticCapabilityContract DescribeCapabilityContract(SecurityExecutionContext security) =>
        Contract() with
        {
            Capabilities = Contract().Capabilities
                .Where(c => SecurityWarrantAuthorization.Allows(
                    security.Warrant,
                    security.Subject,
                    security.Audience,
                    c.Id,
                    c.Operation,
                    security.Tenant,
                    security.ResourceScope))
                .ToArray()
        };

    public SemanticVersionSet DescribeVersionSet() => throw NotUsed(nameof(DescribeVersionSet));
    public DryRunResult DryRun(Foundgine.Semantics.SemanticRequest request) => throw NotUsed(nameof(DryRun));
    public Foundgine.PlanApproval ApprovePlan(Foundgine.Semantics.SemanticRequest request, string approvedBy) => throw NotUsed(nameof(ApprovePlan));
    public Task<ExecutionResult> ExecuteApprovedAsync(Foundgine.PlanApproval approval, ExecutionContext? context = null, CancellationToken cancellationToken = default) => throw NotUsed(nameof(ExecuteApprovedAsync));
    public Task<ExecutionResult> ExecuteAsync(Foundgine.Semantics.SemanticRequest request, ExecutionContext? context = null, CancellationToken cancellationToken = default) => throw NotUsed(nameof(ExecuteAsync));
    public Task<ExecutionResult> ExecuteAsync(Foundgine.Semantics.Intent.ReadIntent intent, ExecutionContext? context = null, CancellationToken cancellationToken = default) => throw NotUsed(nameof(ExecuteAsync));

    private static SemanticCapabilityContract Contract() => new(
        1,
        [
            new("orders.read", "Read Orders", new EntityId(1), AuthorizationDecision.Allowed, [], [], [], [], []) { Operation = "read" },
            new("customers.read", "Read Customers", new EntityId(2), AuthorizationDecision.Allowed, [], [], [], [], []) { Operation = "read" }
        ]);

    private static InvalidOperationException NotUsed(string member) =>
        new($"{nameof(CapabilityOnlyFoundgine)}.{member} is intentionally unavailable in capability-discovery tests.");
}
