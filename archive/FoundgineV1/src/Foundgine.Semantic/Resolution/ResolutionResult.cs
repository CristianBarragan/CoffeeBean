using Foundgine.Metadata;

namespace Foundgine.Semantic.Resolution;

/// <summary>
/// One fact the resolver looked at while trying to resolve a reference --
/// e.g. "Searched Customer.Name for 'Ada Lovelace' using Fuzzy strategy: 1
/// match(es)." Milestone 2 requires resolution to report "evidence used";
/// this is that evidence, one entry per lookup performed, regardless of
/// whether the resolution ultimately succeeded.
/// </summary>
public sealed record ResolutionEvidence(string Description);

/// <summary>
/// The three shapes a resolution attempt can end in. Milestone 2 is
/// explicit that a resolver "must never silently invent an identity", so
/// failure is never collapsed into a single case: zero candidates is
/// <see cref="NotFound"/>, more than one is <see cref="Ambiguous"/> --
/// both distinct from a genuine <see cref="Resolved"/>.
/// </summary>
public enum ResolutionOutcome
{
    Resolved,
    Ambiguous,
    NotFound
}

/// <summary>
/// A successfully resolved domain reference: which entity type, which
/// identity, and why the resolver believes it -- confidence, a
/// human-readable reason, and the evidence that led there. Every field
/// here traces back to something an <see cref="ICandidateSource"/>
/// actually returned; nothing is guessed.
/// </summary>
public sealed record ResolvedReference(
    EntityId EntityType,
    string IdentityValue,
    double Confidence,
    string Reason,
    IReadOnlyList<ResolutionEvidence> Evidence);

/// <summary>
/// The outcome of one <see cref="EntityResolver"/> call. Not just a
/// nullable <see cref="ResolvedReference"/> -- both failure cases
/// (<see cref="ResolutionOutcome.Ambiguous"/>,
/// <see cref="ResolutionOutcome.NotFound"/>) still carry the evidence the
/// resolver gathered, so a caller can see *why* it couldn't resolve, not
/// just that it didn't. There is no public constructor; use the factory
/// methods so an instance can never claim to be <see cref="Resolved"/>
/// while carrying an <see cref="UnresolvedReason"/>, or vice versa.
/// </summary>
public sealed record ResolutionResult
{
    public ResolutionOutcome Outcome { get; }
    public ResolvedReference? Resolved { get; }
    public string? UnresolvedReason { get; }
    public IReadOnlyList<ResolutionEvidence> Evidence { get; }

    private ResolutionResult(
        ResolutionOutcome outcome,
        ResolvedReference? resolved,
        string? unresolvedReason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        Outcome = outcome;
        Resolved = resolved;
        UnresolvedReason = unresolvedReason;
        Evidence = evidence;
    }

    public static ResolutionResult Success(ResolvedReference reference) =>
        new(ResolutionOutcome.Resolved, reference, null, reference.Evidence);

    public static ResolutionResult Ambiguous(string reason, IReadOnlyList<ResolutionEvidence> evidence) =>
        new(ResolutionOutcome.Ambiguous, null, reason, evidence);

    public static ResolutionResult NotFound(string reason, IReadOnlyList<ResolutionEvidence> evidence) =>
        new(ResolutionOutcome.NotFound, null, reason, evidence);
}
