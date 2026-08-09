using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;

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

    /// <summary>
    /// Turns a logical, provider-agnostic <see cref="MutationPlan"/> into a
    /// physical, SQL-specific <see cref="ProviderMutationPlan"/> — the
    /// mutation counterpart of <see cref="Compile(QueryPlan)"/>. Same 1:1
    /// structural translation, not an optimizer.
    /// </summary>
    public static ProviderMutationPlan CompileMutation(MutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var operations = plan.Operations
            .Select(CompileMutationOperation)
            .ToArray();

        return new ProviderMutationPlan(operations);
    }

    private static ProviderMutationNode CompileMutationOperation(MutationOperation operation) => operation switch
    {
        EntityMutation { Kind: MutationKind.Create } create =>
            new SqlInsertNode(create.Entity, create.Columns),

        EntityMutation { Kind: MutationKind.Update, Filter: null } update =>
            throw UnfilteredMutation(update.Kind, update.Entity),

        EntityMutation { Kind: MutationKind.Update } update =>
            new SqlUpdateNode(update.Entity, update.Columns, update.Filter!),

        EntityMutation { Kind: MutationKind.Delete, Filter: null } delete =>
            throw UnfilteredMutation(delete.Kind, delete.Entity),

        EntityMutation { Kind: MutationKind.Delete } delete =>
            new SqlDeleteNode(delete.Entity, delete.Filter!),

        EntityMutation { Kind: MutationKind.Upsert } upsert => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} does not yet support {nameof(MutationKind.Upsert)} " +
            $"(entity '{upsert.Entity.Name}'). INSERT ... ON CONFLICT semantics vary enough by " +
            "SQL dialect that Upsert needs its own compilation path, not implemented yet."),

        GraphMutation graph => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} cannot compile a {nameof(GraphMutation)} (graph " +
            $"'{graph.Graph.GraphName}'): graph-edge mutation isn't representable as a single " +
            "SQL statement. Route this part of the plan through a graph-capable provider instead."),

        RelationshipMutation relationship => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} does not yet support {nameof(RelationshipMutation)} " +
            $"('{relationship.Parent.Name}' -> '{relationship.Child.Name}')."),

        _ => throw new NotSupportedException(
            $"{nameof(SqlPlanCompiler)} does not know how to compile a {operation.GetType().Name}."),
    };

    private static InvalidOperationException UnfilteredMutation(MutationKind kind, EntityMetadata entity) =>
        new(
            $"Cannot compile an unfiltered {kind} on '{entity.Name}': a Filter must identify " +
            "which row(s) to target. Foundgine never mutates every row by accident.");

    private static ProviderNode Compile(QueryNode node) => node switch
    {
        ScanNode scan => new SqlScanNode(scan.Entity),

        JoinNode join => new SqlJoinNode(
            Compile(join.Left),
            Compile(join.Right),
            join.Join),

        CompositeNode composite => CompileComposite(composite),

        ProjectionNode projection => new SqlProjectionNode(
            Compile(projection.Source),
            projection.Fields),

        FilterNode filter => new SqlFilterNode(
            Compile(filter.Source),
            filter.Filter),

        SortNode sort => new SqlSortNode(
            Compile(sort.Source),
            sort.Terms),

        PageNode page => new SqlPageNode(
            Compile(page.Source),
            page.Page),

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

    /// <summary>
    /// Flattens a <see cref="CompositeNode"/> tree into a left-associated
    /// <see cref="SqlJoinNode"/> chain — the relational execution shape SQL
    /// actually needs. This is a deliberate, SQL-specific choice made here,
    /// not something <see cref="Foundgine.Planning.QueryPlanner"/> bakes in
    /// for every provider (that used to be TECH-DEBT-001).
    ///
    /// Each edge's <see cref="CompositeEdge.Child"/> is compiled with its
    /// own <see cref="SqlScanNode"/> instance, and every
    /// <see cref="SqlJoinNode"/> this produces carries explicit
    /// <see cref="SqlJoinNode.LeftOccurrence"/>/<see cref="SqlJoinNode.RightOccurrence"/>
    /// references to the exact two occurrences it joins — not just their
    /// entity type. That's what makes this correct even when the same
    /// entity appears more than once in the tree (e.g. <c>Employee ->
    /// Manager -> Manager</c>): <see cref="SqlTextTranslator"/> resolves
    /// each join's aliases from these occurrence references, so two
    /// <c>Employee</c> scans never collide the way a lookup keyed by
    /// entity alone would.
    /// </summary>
    private static ProviderNode CompileComposite(CompositeNode composite)
    {
        var scan = new SqlScanNode(composite.Entity);
        return AppendChildren(scan, scan, composite.Children);
    }

    private static ProviderNode AppendChildren(
        ProviderNode accumulated,
        SqlScanNode parentOccurrence,
        IReadOnlyList<CompositeEdge> children)
    {
        foreach (var edge in children)
        {
            var childOccurrence = new SqlScanNode(edge.Child.Entity);
            accumulated = new SqlJoinNode(accumulated, childOccurrence, edge.Join, parentOccurrence, childOccurrence);
            accumulated = AppendChildren(accumulated, childOccurrence, edge.Child.Children);
        }

        return accumulated;
    }
}