using Foundgine.Semantic.Resolution;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// What <see cref="ActionPlanner"/> produces from an <see cref="ActionIntent"/>:
/// the fully-resolved chain it walked to get there (Milestone 2's
/// <see cref="ResolvedReference"/>s, anchor-to-target order), the single
/// <see cref="Target"/> the action will act on, which
/// <see cref="Foundgine.Semantic.ActionDescriptor"/> was selected, and the
/// validated arguments for it. This is the "Validate IssueRefund" step in
/// Milestone 4's diagram -- still no policy decision (Milestone 5), no
/// execution plan (Milestone 6/7): just a domain reference and an action
/// that both provably exist, with arguments that provably match what the
/// action declares.
///
/// <see cref="Arguments"/> is keyed by <see cref="Foundgine.Semantic.ActionParameter.Name"/>
/// and contains only parameters that were actually supplied -- an
/// optional parameter the caller omitted simply isn't a key here, rather
/// than being present with a null placeholder.
/// </summary>
public sealed record ResolvedAction(
    IReadOnlyList<ResolvedReference> AnchorChain,
    ResolvedReference Target,
    ActionDescriptor Action,
    IReadOnlyDictionary<string, object?> Arguments,
    IReadOnlyList<ResolutionEvidence> Evidence);

/// <summary>
/// The outcome of one <see cref="ActionPlanner.Plan"/> call. Mirrors
/// <see cref="Intent.ReadPlanResult"/>'s shape for the same reason: an
/// action intent can fail to resolve or fail to validate at any step, and
/// the caller needs to know why, with evidence, rather than getting a
/// half-built plan or a thrown exception for an ordinary "not found" or
/// "no such action".
/// </summary>
public sealed record ActionPlanResult
{
    public bool IsResolved { get; }
    public ResolvedAction? Action { get; }
    public string? UnresolvedReason { get; }
    public IReadOnlyList<ResolutionEvidence> Evidence { get; }

    private ActionPlanResult(
        bool isResolved,
        ResolvedAction? action,
        string? unresolvedReason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        IsResolved = isResolved;
        Action = action;
        UnresolvedReason = unresolvedReason;
        Evidence = evidence;
    }

    public static ActionPlanResult Success(ResolvedAction action) =>
        new(true, action, null, action.Evidence);

    public static ActionPlanResult Unresolved(string reason, IReadOnlyList<ResolutionEvidence> evidence) =>
        new(false, null, reason, evidence);
}
