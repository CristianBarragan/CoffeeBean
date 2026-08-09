using Foundgine.Builders;
using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// What a caller wants resolved, expressed purely in terms of
/// <see cref="Foundgine.Metadata"/> identities — never in terms of tables,
/// SQL, or any other physical concept. <see cref="QueryPlanner"/> is the
/// only thing that turns this into a <see cref="QueryPlan"/>.
///
/// <see cref="Branches"/> is a tree, not a flat chain: each
/// <see cref="QueryIntentBranch"/> is reached from <see cref="Root"/> (or
/// from its own parent branch) via whatever edge <see cref="JoinGraph"/>
/// has registered between the two entities. This is what lets an intent
/// express fan-out —
///
/// <code>
/// Customer
/// ├── Accounts
/// │    └── Transactions
/// └── ContactPoints
/// </code>
///
/// — and not only a single linear path. The purely linear case (this
/// first E2E's Customer -> Account -> Transaction) is just a tree where
/// every branch has at most one child; <see cref="Linear"/> below builds
/// exactly that shape so simple call sites don't need to construct nested
/// <see cref="QueryIntentBranch"/> records by hand.
/// </summary>
public sealed record QueryIntent(
    EntityId Root,
    IReadOnlyList<QueryIntentBranch> Branches,
    IReadOnlyList<FieldBinding>? Fields = null,
    FilterExpression? Filter = null,
    IReadOnlyList<SortTerm>? Sort = null,
    PageSpec? Page = null
)
{
    /// <summary>
    /// Builds a <see cref="QueryIntent"/> for the common linear case:
    /// <c>Root -> path[0] -> path[1] -> ...</c>, with no fan-out. This is
    /// the shape every intent had before branching existed, kept as a
    /// convenience rather than the only shape available.
    /// </summary>
    public static QueryIntent Linear(
        EntityId root,
        IReadOnlyList<EntityId> path,
        IReadOnlyList<FieldBinding>? fields = null,
        FilterExpression? filter = null,
        IReadOnlyList<SortTerm>? sort = null,
        PageSpec? page = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        QueryIntentBranch? tail = null;
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var children = tail is null ? null : new[] { tail };
            tail = new QueryIntentBranch(path[i], children);
        }

        var branches = tail is null
            ? Array.Empty<QueryIntentBranch>()
            : new[] { tail };

        return new QueryIntent(root, branches, fields, filter, sort, page);
    }
}
