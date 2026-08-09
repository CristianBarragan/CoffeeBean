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
/// <see cref="QueryIntent.Branches"/> is a tree, so this walks it
/// depth-first, threading a single accumulating <see cref="QueryNode"/>
/// through every branch it visits (siblings included). That works because
/// a <see cref="JoinNode"/>'s condition names its two entities explicitly
/// (see <see cref="Foundgine.Metadata.JoinCondition"/>) rather than
/// depending on where in the tree it sits — so which existing alias a new
/// join attaches to is resolved later, by entity identity, not by tree
/// shape. The planner therefore doesn't need a richer "fan-out" QueryNode
/// to represent branching; a left-associated chain of JoinNodes already
/// compiles to the correct SQL FROM/JOIN clause for a branching intent, as
/// long as every entity a join condition references was scanned somewhere
/// earlier in that chain — which a depth-first walk guarantees.
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

        var rootEntity = GetEntityOrThrow(intent.Root);
        QueryNode node = new ScanNode(rootEntity);

        node = PlanBranches(node, intent.Root, intent.Branches);

        if (intent.Fields is { Count: > 0 } fields)
            node = new ProjectionNode(node, fields);

        return new QueryPlan(node);
    }

    private QueryNode PlanBranches(
        QueryNode node,
        EntityId parent,
        IReadOnlyList<QueryIntentBranch> branches)
    {
        foreach (var branch in branches)
        {
            if (!_joinGraph.TryGetJoin(parent, branch.Entity, out var join))
            {
                var parentName = GetEntityOrThrow(parent).Name;
                var childName = GetEntityOrThrow(branch.Entity).Name;

                throw new InvalidOperationException(
                    $"Cannot plan '{parentName}' -> '{childName}': no relationship is " +
                    $"registered between them in the {nameof(JoinGraph)}. The planner only " +
                    "discovers relationships that metadata already knows about — it never " +
                    "guesses one. Register a JoinGraph edge between these entities first.");
            }

            var childEntity = GetEntityOrThrow(branch.Entity);
            node = new JoinNode(node, new ScanNode(childEntity), join);

            if (branch.Children is { Count: > 0 } children)
                node = PlanBranches(node, branch.Entity, children);
        }

        return node;
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
