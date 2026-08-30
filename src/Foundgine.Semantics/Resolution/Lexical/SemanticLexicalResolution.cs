using Foundgine.Abstractions;

namespace Foundgine.Semantics.Resolution;

public enum SemanticLexicalResolutionOutcome : byte
{
    Resolved,
    Ambiguous,
    Unresolved
}

/// <summary>One selected lexical interpretation and its graph path.</summary>
public sealed record SemanticLexicalStep(
    string Token,
    SemanticLexicalCandidate Candidate,
    double PathScore,
    IReadOnlyList<SemanticLexicalCandidate> BridgingPath);

/// <summary>Result of schema-constrained lexical inference.</summary>
public sealed record SemanticLexicalResolution(
    SemanticLexicalResolutionOutcome Outcome,
    IReadOnlyList<SemanticLexicalStep> Steps,
    double Confidence,
    EntityId? RootEntity,
    string? Reason,
    IReadOnlyList<SemanticLexicalCandidate>? RootCandidates = null)
{
    public IReadOnlyList<SemanticLexicalCandidate> EffectiveRootCandidates => RootCandidates ?? [];
}
