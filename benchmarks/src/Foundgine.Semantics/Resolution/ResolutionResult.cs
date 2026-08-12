using Foundgine.Abstractions;

namespace Foundgine.Semantics.Resolution;

public sealed record ResolutionEvidence(string Description);

public enum ResolutionOutcome
{
    Resolved,
    Ambiguous,
    NotFound
}

public sealed record ResolvedReference(
    EntityId EntityType,
    string IdentityValue,
    double Confidence,
    string Reason,
    IReadOnlyList<ResolutionEvidence> Evidence);

public sealed record ResolutionResult
{
    public ResolutionOutcome Outcome { get; }
    public ResolvedReference? Resolved { get; }
    public string? UnresolvedReason { get; }
    public IReadOnlyList<ResolutionEvidence> Evidence { get; }

    private ResolutionResult(
        ResolutionOutcome outcome,
        ResolvedReference? resolved,
        string? reason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        Outcome = outcome;
        Resolved = resolved;
        UnresolvedReason = reason;
        Evidence = evidence;
    }

    public static ResolutionResult Success(ResolvedReference reference) =>
        new(ResolutionOutcome.Resolved, reference, null, reference.Evidence);

    public static ResolutionResult Ambiguous(
        string reason,
        IReadOnlyList<ResolutionEvidence> evidence) =>
        new(ResolutionOutcome.Ambiguous, null, reason, evidence);

    public static ResolutionResult NotFound(
        string reason,
        IReadOnlyList<ResolutionEvidence> evidence) =>
        new(ResolutionOutcome.NotFound, null, reason, evidence);
}
