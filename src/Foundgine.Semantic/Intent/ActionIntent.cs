using Foundgine.Metadata;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// One named argument to an <see cref="ActionIntent"/>, e.g.
/// <c>("Amount", 25m)</c> for "I want to refund Ada $25". Values are
/// validated against the target <see cref="ActionDescriptor"/>'s declared
/// <see cref="ActionParameter"/> list by <see cref="ActionPlanner"/> --
/// never invoked speculatively.
/// </summary>
public sealed record ActionArgument(string ParameterName, object? Value);

/// <summary>
/// Milestone 4: a domain-action request expressed structurally -- the
/// action-pipeline counterpart of <see cref="ReadIntent"/>. Same
/// discipline: not natural-language text, and not yet resolved. This is
/// the "AI" step in the milestone's own diagram:
///
/// <code>
/// AI:
/// "I want to refund Ada $25"
///        ↓
/// Foundgine:
/// Resolve Customer
/// Resolve Transaction
/// Validate IssueRefund
/// Check policy
/// Build plan
/// </code>
///
/// The milestone's acceptance example maps onto this shape as:
/// <c>AnchorEntity=Customer</c>, <c>AnchorPhrase="Ada"</c>,
/// <c>ActionName="IssueRefund"</c>, <c>Arguments=[("Amount", 25m)]</c>.
///
/// <see cref="TargetRelationship"/> exists for the shape "her last
/// transaction" -- an action target that isn't the resolved anchor chain
/// itself, but the single most-recent instance reached from it via a
/// to-many relationship (e.g. Account -&gt; Transactions, ordered by date
/// descending). Leave it null when the action targets the anchor chain's
/// final entity directly (e.g. IssueRefund targeting a Customer).
///
/// <see cref="ActionPlanner"/> is the only thing that turns this into a
/// <see cref="ResolvedAction"/> -- it never executes anything itself;
/// checking policy (Milestone 5) and actually running the action
/// (Milestone 6/7) are later, separate stages this type deliberately
/// says nothing about.
/// </summary>
public sealed record ActionIntent(
    EntityId AnchorEntity,
    string AnchorPhrase,
    IReadOnlyList<string> ThroughRelationships,
    string ActionName,
    IReadOnlyList<ActionArgument> Arguments,
    string? TargetRelationship = null,
    FieldId? TargetOrderBy = null,
    bool TargetDescending = true);
