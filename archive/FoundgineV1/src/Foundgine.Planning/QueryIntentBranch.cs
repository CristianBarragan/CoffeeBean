using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// One entity reached from its parent in a <see cref="QueryIntent"/>'s
/// request shape, plus whatever further branches hang off of it.
///
/// This is what turns <see cref="QueryIntent"/> from a flat chain into a
/// tree: a parent can list more than one <see cref="QueryIntentBranch"/>,
/// which is how a caller expresses fan-out such as
///
/// <code>
/// Customer
/// ├── Accounts
/// │    └── Transactions
/// └── ContactPoints
/// </code>
///
/// instead of only a single linear path like <c>Customer -> Account ->
/// Transaction</c>. <see cref="QueryPlanner"/> is the only thing that
/// interprets this — a branch says nothing about tables, SQL, or any other
/// physical concept, only "this entity, reached from its parent, expressed
/// as a metadata identity".
/// </summary>
public sealed record QueryIntentBranch(
    EntityId Entity,
    IReadOnlyList<QueryIntentBranch>? Children = null
);
