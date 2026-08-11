using Foundgine.Metadata;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// Milestone 3: a read request expressed structurally -- not natural
/// language text, and not yet resolved. This is the "Intent" step in the
/// milestone's own diagram:
///
/// <code>
/// Natural language
///       ↓
///     Intent
///       ↓
///     Resolve
///       ↓
/// Semantic query
///       ↓
/// Foundgine QueryPlan
/// </code>
///
/// Turning natural language into one of these is explicitly out of scope
/// for Foundgine itself: "The LLM is an optional reasoning client.
/// Foundgine owns the constrained semantic representation and execution."
/// A <see cref="ReadIntent"/> *is* that constrained representation -- the
/// contract an LLM, a parser, or a hand-written caller is expected to
/// produce, and the only thing <see cref="ReadPlanner"/> accepts.
///
/// The milestone's own acceptance example --
///
/// <code>
/// Find Ada's last five transactions.
///     ↓
/// Resolve Customer
///     ↓
/// Resolve Account through Customer relationship
///     ↓
/// Query Transaction ordered by transaction identity/time
///     ↓
/// Limit 5
/// </code>
///
/// -- maps onto this shape as: <c>AnchorEntity=Customer</c>,
/// <c>AnchorPhrase="Ada"</c>, <c>ThroughRelationships=["Accounts"]</c>,
/// <c>TargetRelationship="Transactions"</c>, <c>Descending=true</c>,
/// <c>Limit=5</c>.
/// </summary>
public sealed record ReadIntent(
    EntityId AnchorEntity,
    string AnchorPhrase,
    IReadOnlyList<string> ThroughRelationships,
    string TargetRelationship,
    FieldId? OrderBy = null,
    bool Descending = false,
    int? Limit = null);
