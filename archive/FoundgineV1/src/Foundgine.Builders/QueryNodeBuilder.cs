using Foundgine.Metadata;

namespace Foundgine.Builders;

/// <summary>
/// Generic (hand-written, not generated) helpers for turning
/// Foundation.Metadata into a QueryNode tree. Generated planners
/// (GeneratedPlanners.*.BuildQueryNode) call into these instead of each
/// re-deriving foreign-key/join resolution themselves.
/// </summary>
public static class QueryNodeBuilder
{
    /// <summary>
    /// Builds the Scan/Join chain for a model's own composite entities,
    /// using the ordered ModelEntityBinding list ModelMetadata already
    /// carries (primary entity first, JoinToParent == null; every
    /// subsequent entity carries the join condition back to its parent).
    /// For a non-composite model this just returns a single ScanNode.
    /// </summary>
    public static QueryNode ScanComposite(ModelMetadata model)
    {
        QueryNode? plan = null;

        foreach (var binding in model.Entities)
        {
            plan = binding.JoinToParent is null
                ? new ScanNode(binding.Entity)
                : new JoinNode(
                    plan!,
                    new ScanNode(binding.Entity),
                    new JoinMetadata(binding.JoinToParent, JoinKind.Left));
        }

        return plan ??
            throw new System.InvalidOperationException(
                $"Model '{model.Name}' has no entities to scan.");
    }
}
