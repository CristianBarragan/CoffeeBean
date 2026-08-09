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
/// Instead, for each step in <see cref="QueryIntent.Path"/> it asks the
/// <see cref="JoinGraph"/> "does a relationship exist from A to B?" and the
/// <see cref="MetadataRegistry"/> "what does entity X look like?". Point it
/// at a different domain's metadata and it plans that domain's queries
/// exactly the same way, with no code changes.
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

        var current = intent.Root;
        foreach (var next in intent.Path)
        {
            if (!_joinGraph.TryGetJoin(current, next, out var join))
            {
                var currentName = GetEntityOrThrow(current).Name;
                var nextName = GetEntityOrThrow(next).Name;

                throw new InvalidOperationException(
                    $"Cannot plan '{currentName}' -> '{nextName}': no relationship is " +
                    $"registered between them in the {nameof(JoinGraph)}. The planner only " +
                    "discovers relationships that metadata already knows about — it never " +
                    "guesses one. Register a JoinGraph edge between these entities first.");
            }

            var nextEntity = GetEntityOrThrow(next);
            node = new JoinNode(node, new ScanNode(nextEntity), join);
            current = next;
        }

        if (intent.Fields is { Count: > 0 } fields)
            node = new ProjectionNode(node, fields);

        return new QueryPlan(node);
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
