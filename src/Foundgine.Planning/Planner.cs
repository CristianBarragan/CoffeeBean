using Foundgine.Abstractions;
using Foundgine.Semantics;

namespace Foundgine.Planning;

/// <summary>
/// Turns an authorized semantic graph into a provider-independent execution
/// tree. This planner does not discover relationships, resolve storage, or
/// choose a physical provider strategy; those concerns belong to earlier and
/// later layers respectively.
/// </summary>
public sealed class Planner : IPlanner
{
    public ExecutionPlan Plan(SemanticGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.Nodes.Count == 0)
            throw new InvalidOperationException("Cannot plan an empty semantic graph.");

        var byParent = graph.Nodes.ToLookup(node => node.ParentId);

        var roots = byParent[null].ToArray();
        if (roots.Length != 1)
        {
            throw new InvalidOperationException(
                "An execution plan requires exactly one root semantic node.");
        }

        var nodeIds = graph.Nodes.Select(node => node.Id).ToHashSet();
        var visited = new HashSet<int>();
        var root = BuildNode(roots[0], byParent, nodeIds, visited, graph.Options, isRoot: true);

        if (visited.Count != graph.Nodes.Count)
        {
            var unreachable = graph.Nodes
                .Where(node => !visited.Contains(node.Id))
                .Select(node => node.Id);

            throw new InvalidOperationException(
                $"Semantic graph contains unreachable nodes: {string.Join(", ", unreachable)}.");
        }

        return new ExecutionPlan(root);
    }

    private static ExecutionPlanNode BuildNode(
        SemanticGraphNode semanticNode,
        ILookup<int?, SemanticGraphNode> byParent,
        IReadOnlySet<int> nodeIds,
        ISet<int> visited,
        Foundgine.Semantics.Query.SemanticQueryOptions? queryOptions,
        bool isRoot)
    {
        if (!visited.Add(semanticNode.Id))
        {
            throw new InvalidOperationException(
                $"Semantic graph contains a cycle at node {semanticNode.Id}.");
        }

        if (!isRoot && semanticNode.ViaRelationship is null)
        {
            throw new InvalidOperationException(
                $"Non-root semantic node {semanticNode.Id} must specify the relationship used to reach it.");
        }

        if (isRoot && semanticNode.ViaRelationship is not null)
        {
            throw new InvalidOperationException(
                $"Root semantic node {semanticNode.Id} cannot specify a parent relationship.");
        }

        if (semanticNode.ParentId is { } parentId && !nodeIds.Contains(parentId))
        {
            throw new InvalidOperationException(
                $"Semantic node {semanticNode.Id} references missing parent node {parentId}.");
        }

        var operation = isRoot
            ? ExecutionOperation.Scan
            : ExecutionOperation.Traverse;

        var children = byParent[semanticNode.Id]
            .Select(child => BuildNode(child, byParent, nodeIds, visited, queryOptions, isRoot: false))
            .ToArray();

        return new ExecutionPlanNode(
            semanticNode.Id,
            operation,
            semanticNode.EntityId,
            semanticNode.Fields,
            semanticNode.ViaRelationship,
            children,
            isRoot ? queryOptions : null);
    }
}
