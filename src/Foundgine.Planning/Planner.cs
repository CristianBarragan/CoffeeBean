using Foundgine.Abstractions;
using Foundgine.Semantics;
using Foundgine.Semantics.IR;

namespace Foundgine.Planning;

/// <summary>
/// Turns authorized Semantic IR into the canonical semantic planning
/// artifact. The resulting SemanticPlan is lowered to ExecutionIR separately. This planner does not discover relationships, resolve storage, or
/// choose a physical provider strategy; those concerns belong to earlier and
/// later layers respectively.
/// </summary>
public sealed class Planner : IPlanner
{
    public SemanticPlan Plan(SemanticOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var visited = new HashSet<int>();
        var root = BuildNode(operation.Root, visited, isRoot: true);
        return new SemanticPlan(root);
    }

    /// <summary>
    /// Compatibility adapter. New orchestration code should compile the graph
    /// to canonical Semantic IR before invoking the planner.
    /// </summary>
    public SemanticPlan Plan(SemanticGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Plan(SemanticOperationCompiler.Compile(graph));
    }

    private static SemanticPlanNode BuildNode(
        SemanticReadNode semanticNode,
        ISet<int> visited,
        bool isRoot)
    {
        if (!visited.Add(semanticNode.Id))
            throw new InvalidOperationException(
                $"Semantic operation contains a cycle or duplicate node at {semanticNode.Id}.");

        if (!isRoot && semanticNode.ViaRelationship is null && semanticNode.ViaConnection is null)
            throw new InvalidOperationException(
                $"Non-root semantic node {semanticNode.Id} must specify the relationship or connection used to reach it.");

        if (semanticNode.ViaRelationship is not null && semanticNode.ViaConnection is not null)
            throw new InvalidOperationException(
                $"Semantic node {semanticNode.Id} cannot specify both a relationship and a connection.");

        if (isRoot && (semanticNode.ViaRelationship is not null || semanticNode.ViaConnection is not null))
            throw new InvalidOperationException(
                $"Root semantic node {semanticNode.Id} cannot specify a parent edge.");

        var operation = isRoot
            ? ExecutionOperation.Scan
            : semanticNode.ViaConnection is not null
                ? ExecutionOperation.TraverseConnection
                : ExecutionOperation.Traverse;

        var children = semanticNode.Children
            .Select(child => BuildNode(child, visited, isRoot: false))
            .ToArray();

        return new SemanticPlanNode(
            semanticNode.Id,
            operation,
            semanticNode.EntityId,
            semanticNode.Fields,
            semanticNode.ViaRelationship,
            semanticNode.ViaConnection,
            children,
            isRoot ? semanticNode.QueryOptions : null,
            semanticNode.Authorization);
    }
}
