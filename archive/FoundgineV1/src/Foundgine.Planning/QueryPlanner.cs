using Foundgine.Builders;
using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// Turns a <see cref="QueryIntent"/> into a <see cref="QueryPlan"/> by
/// consulting <see cref="Foundgine.Metadata"/> — never by hardcoding
/// domain-specific rules.
///
/// This is the "dynamic planner" the architecture review's Section 5
/// describes: it does not contain <c>if Customer then join Accounts</c>.
/// Instead, for each <see cref="QueryIntentBranch"/> it asks the
/// <see cref="JoinGraph"/> "does a relationship exist from the parent to
/// this entity?" and the <see cref="MetadataRegistry"/> "what does entity
/// X look like?". Point it at a different domain's metadata and it plans
/// that domain's queries exactly the same way, with no code changes.
///
/// <see cref="QueryIntent.Branches"/> is a tree, and the output
/// <see cref="CompositeNode"/> keeps that exact shape — this planner does
/// NOT flatten it into a relational join chain. That used to happen here;
/// it was TECH-DEBT-001, because a plan that's already been flattened into
/// <c>(((Customer JOIN Account) JOIN Transaction) JOIN ContactPoint)</c>
/// has thrown away information a non-SQL provider (graph traversal, a
/// cache, a smarter join-reordering compiler) would need. Flattening is
/// now <see cref="Foundgine.Providers.SqlPlanCompiler"/>'s job, made at SQL
/// compile time from the still-intact tree.
/// </summary>
public sealed class QueryPlanner
{
    private readonly MetadataRegistry _metadata;
    private readonly JoinGraph _joinGraph;

    public QueryPlanner(MetadataRegistry metadata, JoinGraph joinGraph)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _joinGraph = joinGraph ?? throw new ArgumentNullException(nameof(joinGraph));
    }

    public QueryPlan Plan(QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        QueryNode node = PlanComposite(intent.Root, intent.Branches);

        // Order matters here only in that it mirrors SQL clause order
        // (WHERE, then ORDER BY, then LIMIT/OFFSET, then column selection)
        // for readability — SqlTextTranslator unwraps these regardless of
        // nesting order, so a different order here would compile to the
        // same SQL.
        if (intent.Filter is { } filter)
            node = new FilterNode(node, filter);

        if (intent.Sort is { Count: > 0 } sort)
            node = new SortNode(node, sort);

        if (intent.Page is { } page)
            node = new PageNode(node, page);

        if (intent.Fields is { Count: > 0 } fields)
            node = new ProjectionNode(node, fields);

        return new QueryPlan(node);
    }

    private CompositeNode PlanComposite(EntityId entityId, IReadOnlyList<QueryIntentBranch> branches)
    {
        var entity = GetEntityOrThrow(entityId);
        var edges = new List<CompositeEdge>(branches.Count);

        foreach (var branch in branches)
        {
            if (!_joinGraph.TryGetJoin(entityId, branch.Entity, out var join))
            {
                var parentName = entity.Name;
                var childName = GetEntityOrThrow(branch.Entity).Name;

                throw new InvalidOperationException(
                    $"Cannot plan '{parentName}' -> '{childName}': no relationship is " +
                    $"registered between them in the {nameof(JoinGraph)}. The planner only " +
                    "discovers relationships that metadata already knows about — it never " +
                    "guesses one. Register a JoinGraph edge between these entities first.");
            }

            var childComposite = PlanComposite(branch.Entity, branch.Children ?? Array.Empty<QueryIntentBranch>());
            edges.Add(new CompositeEdge(join, childComposite));
        }

        return new CompositeNode(entity, edges);
    }

    private EntityMetadata GetEntityOrThrow(EntityId id)
    {
        if (!_metadata.TryGet(id, out var entity))
        {
            throw new InvalidOperationException(
                $"Cannot plan a query over entity id {id.Value}: it is not registered in the " +
                $"{nameof(MetadataRegistry)}. The planner can only reason about entities that " +
                "domain metadata has described.");
        }

        return entity;
    }
}