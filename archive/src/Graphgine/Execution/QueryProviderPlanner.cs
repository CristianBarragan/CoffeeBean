using Foundgine.Execution.Contracts;
using FoundationQuery = Foundgine.Builders;

namespace Graphgine.Execution;

/// <summary>
/// Lowers a Foundation logical QueryNode tree into a
/// Foundgine.Execution.Contracts.ProviderPlan. This is the "Provider Plan"
/// layer the target pipeline calls for:
///
///   HotChocolate -> SelectionIR -> Foundation QueryNode -> Provider Plan
///     -> SQL/Graph Compiler -> Execution Provider
///
/// Before this existed, ProviderPlan/ProviderNode were pure "architectural
/// theater" -- real types with nothing anywhere in the repo that produced
/// one. QueryPlanTranslator used to go straight from QueryNode to
/// PhysicalQueryPlan, skipping the provider-plan layer entirely.
///
/// This planner is deliberately a plain structural lowering right now --
/// every QueryNode shape maps onto exactly one ProviderNode shape, chosen
/// for SQL/AGE (the only backends this repo has). A real "provider
/// planner" earns its name once it actually *chooses* between strategies
/// (e.g. "serve this subtree from cache instead" -> CacheLookupNode) --
/// that decision-making is intentionally not attempted here; see
/// Foundgine.Providers.CacheExecutionProvider, which is still a stub for
/// the same reason. Today this planner's only real job is making the
/// QueryNode -> ProviderPlan seam actually exist and actually get used,
/// so QueryPlanTranslator's second stage (ProviderPlan -> PhysicalQueryPlan)
/// has a real, non-degenerate input.
///
/// MaterializeNode has no ProviderNode counterpart and is unwrapped here:
/// its only payload (ModelMetadata) was already unused by the translator
/// (it never read `.Model` -- see git history), and the model-scoped id it
/// would have supplied is now available directly off EntityMetadata.OwningModel.
/// Introducing a "MaterializeProviderNode" purely to preserve a shape nothing
/// consumes would be exactly the kind of unearned abstraction the review
/// this work is based on warned against adding more of.
/// </summary>
public static class QueryProviderPlanner
{
    public static ProviderPlan Plan(FoundationQuery.QueryNode root) =>
        new(PlanNode(root));

    private static ProviderNode PlanNode(FoundationQuery.QueryNode node) =>
        node switch
        {
            FoundationQuery.ScanNode scan =>
                new SqlScanNode(scan.Entity),

            FoundationQuery.JoinNode join =>
                new SqlJoinNode(
                    PlanNode(join.Left),
                    PlanNode(join.Right),
                    join.Join),

            FoundationQuery.GraphEdgeNode graphEdge =>
                new GraphTraversalNode(
                    PlanNode(graphEdge.Source),
                    graphEdge.Graph,
                    graphEdge.From is null ? null : PlanNode(graphEdge.From),
                    graphEdge.To is null ? null : PlanNode(graphEdge.To)),

            FoundationQuery.ProjectionNode projection =>
                new SqlProjectionNode(
                    PlanNode(projection.Source),
                    projection.Fields),

            // See class remarks: MaterializeNode carries nothing this
            // layer needs, so it's a pass-through rather than a node.
            FoundationQuery.MaterializeNode materialize =>
                PlanNode(materialize.Source),

            _ => throw new System.NotSupportedException(
                $"QueryProviderPlanner: unsupported QueryNode '{node.GetType().Name}'.")
        };
}
