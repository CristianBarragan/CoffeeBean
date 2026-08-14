using Foundgine.Abstractions;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Query;

namespace Foundgine.Execution;

/// <summary>
/// Canonical provider-neutral execution representation.
///
/// Semantic IR answers what the operation means. Execution IR answers what
/// provider-neutral work must be performed. It deliberately contains no SQL,
/// storage names, provider types, aliases, or connection details.
/// </summary>
public sealed record ExecutionIR(ExecutionIRNode Root)
{
    public static ExecutionIR From(SemanticPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ExecutionIR(ExecutionIRNode.From(plan.Root));
    }
}

public sealed record ExecutionIRNode(
    int Id,
    ExecutionOperation Operation,
    EntityId EntityId,
    IReadOnlyList<FieldId> Fields,
    RelationshipId? ViaRelationship,
    ConnectionId? ViaConnection,
    IReadOnlyList<ExecutionIRNode> Children,
    SemanticQueryOptions? QueryOptions = null,
    AuthorizationPredicate? Authorization = null)
{
    internal static ExecutionIRNode From(SemanticPlanNode node) =>
        new(
            node.Id,
            node.Operation,
            node.EntityId,
            node.Fields,
            node.ViaRelationship,
            node.ViaConnection,
            node.Children.Select(From).ToArray(),
            node.QueryOptions,
            node.Authorization);
}

/// <summary>
/// Explicit lowering boundary from the planner's semantic plan to the
/// provider-neutral execution representation.
/// </summary>
public static class ExecutionIRCompiler
{
    public static ExecutionIR Compile(SemanticPlan plan) =>
        ExecutionIR.From(plan);
}
