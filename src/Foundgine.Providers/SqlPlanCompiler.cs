using Foundgine.Builders;
using Foundgine.Execution.Contracts;

namespace Foundgine.Providers;

/// <summary>
/// Turns a logical, provider-agnostic <see cref="QueryPlan"/> (Foundgine.Builders)
/// into a physical, SQL-specific <see cref="ProviderPlan"/> (Foundgine.Execution.Contracts),
/// choosing the SQL node types for every logical node it knows how to
/// represent in SQL.
///
/// This is a 1:1 structural translation, not an optimizer — it exists so
/// the boundary between "what data is needed" (QueryNode) and "how a SQL
/// backend fetches it" (ProviderNode) stays a real seam a second provider
/// (graph, cache, remote API) could occupy later, per the architecture
/// review's Section 6.
/// </summary>
public static class SqlPlanCompiler
{
    public static ProviderPlan Compile(QueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ProviderPlan(Compile(plan.Root));
    }

    private static ProviderNode Compile(QueryNode node) => node switch
    {
        ScanNode scan => new SqlScanNode(scan.Entity),

        JoinNode join => new SqlJoinNode(
            Compile(join.Left),
            Compile(join.Right),
            join.Join),

        ProjectionNode projection => new SqlProjectionNode(
            Compile(projection.Source),
            projection.Fields),

        GraphEdgeNode => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} cannot compile a {nameof(GraphEdgeNode)}: graph-edge " +
            "traversal isn't representable as a single SQL statement. Route this part of the " +
            "plan through a graph-capable provider instead."),

        MaterializeNode => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} does not yet support {nameof(MaterializeNode)}. " +
            "Materialization into ModelMetadata-shaped objects happens after the SQL provider " +
            "returns rows, not as part of the SQL translation itself."),

        _ => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} does not know how to compile a {node.GetType().Name}."),
    };
}
