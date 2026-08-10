using Foundgine.Metadata;
using Foundgine.Semantic.Resolution;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// What <see cref="ReadPlanner"/> produces from a <see cref="ReadIntent"/>:
/// the fully-resolved chain of single-entity references it walked to get
/// there (Milestone 2's <see cref="ResolvedReference"/>s, in anchor-to-leaf
/// order), plus the bulk query left to run at the end -- which entity,
/// which relationship reached it, and the ordering/limit the intent asked
/// for. This is the "Semantic query" step in Milestone 3's diagram: still
/// no SQL, no <c>Foundgine.Builders.QueryPlan</c>, no
/// <c>Foundgine.Execution.Contracts.ProviderPlan</c> -- just
/// <see cref="Foundgine.Metadata"/> identities and
/// <see cref="Foundgine.Semantic"/> descriptors, same as everything
/// upstream of it.
///
/// Translating this into a physical query is a separate, honest gap
/// today: <c>Foundgine.Planning.QueryIntent</c> can express the join shape
/// (<see cref="AnchorChain"/>'s entities plus <see cref="TargetEntity"/>),
/// but nothing downstream of it -- <c>Foundgine.Builders</c>,
/// <c>Foundgine.Planning</c>, <c>Foundgine.Providers</c> -- yet supports
/// filtering by a resolved literal, an <c>ORDER BY</c>, or a
/// <c>LIMIT</c>. <see cref="OrderBy"/>/<see cref="Descending"/>/
/// <see cref="Limit"/> are captured here so that gap has a concrete,
/// typed shape to be closed against, rather than being silently dropped.
/// </summary>
public sealed record ResolvedReadPlan(
    IReadOnlyList<ResolvedReference> AnchorChain,
    EntityId TargetEntity,
    RelationshipId TargetRelationship,
    FieldId? OrderBy,
    bool Descending,
    int? Limit,
    IReadOnlyList<ResolutionEvidence> Evidence);

/// <summary>
/// The outcome of one <see cref="ReadPlanner.Plan"/> call. Mirrors
/// <see cref="ResolutionResult"/>'s shape for the same reason: a read
/// intent can fail to resolve at any step in its anchor chain, and the
/// caller needs to know why, with evidence, rather than getting a
/// half-built plan or a thrown exception for an ordinary "not found".
/// </summary>
public sealed record ReadPlanResult
{
    public bool IsResolved { get; }
    public ResolvedReadPlan? Plan { get; }
    public string? UnresolvedReason { get; }
    public IReadOnlyList<ResolutionEvidence> Evidence { get; }

    private ReadPlanResult(
        bool isResolved,
        ResolvedReadPlan? plan,
        string? unresolvedReason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        IsResolved = isResolved;
        Plan = plan;
        UnresolvedReason = unresolvedReason;
        Evidence = evidence;
    }

    public static ReadPlanResult Success(ResolvedReadPlan plan) => new(true, plan, null, plan.Evidence);

    public static ReadPlanResult Unresolved(string reason, IReadOnlyList<ResolutionEvidence> evidence) =>
        new(false, null, reason, evidence);
}
