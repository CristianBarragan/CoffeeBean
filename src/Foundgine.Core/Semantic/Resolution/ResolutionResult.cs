using Foundgine.Core.Abstractions;

namespace Foundgine.Core.Semantic.Resolution;

public enum CandidateEvidenceKind : byte
{
    ExactIdentity,
    Alias,
    Trigram,
    FullText,
    Bm25,
    GraphSimilarity,
    VectorSimilarity,
    Relationship
}

public sealed record ResolutionEvidence(
    string Description,
    CandidateEvidenceKind? Kind = null,
    double? Score = null);

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

    public ResolutionOutcome Outcome { get; }
    public ResolvedReference? Resolved { get; }
    public string? UnresolvedReason { get; }
    public IReadOnlyList<ResolutionEvidence> Evidence { get; }

    public static ResolutionResult Success(ResolvedReference reference)
    {
        return new ResolutionResult(ResolutionOutcome.Resolved, reference, null, reference.Evidence);
    }

    public static ResolutionResult Ambiguous(
        string reason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        return new ResolutionResult(ResolutionOutcome.Ambiguous, null, reason, evidence);
    }

    public static ResolutionResult NotFound(
        string reason,
        IReadOnlyList<ResolutionEvidence> evidence)
    {
        return new ResolutionResult(ResolutionOutcome.NotFound, null, reason, evidence);
    }
}