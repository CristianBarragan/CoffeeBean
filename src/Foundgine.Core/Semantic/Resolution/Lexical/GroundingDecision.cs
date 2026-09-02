using Foundgine.Core.Abstractions;

namespace Foundgine.Core.Semantic.Resolution;

/// <summary>
/// Outcome of a grounding decision. A grounding decision is not "did the
/// tokens map onto a legal path" — the graph-constrained search already
/// answers that. It is "how many distinct <em>meanings</em> were legal, and
/// is Foundgine justified in silently picking one of them."
/// </summary>
public enum GroundingOutcome : byte
{
    /// <summary>A single interpretation was constructed, or several
    /// interpretations were constructed but they agree on meaning (they
    /// differ only in the bridging route through the graph, not in what
    /// the expression maps to).</summary>
    Committed,

    /// <summary>Two or more interpretations are structurally valid and
    /// disagree on meaning — a different field, value, relationship, or
    /// root entity. Foundgine must not authorize one of them silently.</summary>
    RequiresClarification,

    /// <summary>No legal interpretation could be constructed at all.</summary>
    Unresolved,

    /// <summary>The search was stopped by a configured resource limit
    /// (token count, total paths explored, elapsed search time, retrieval
    /// time, or a cancelled <see cref="System.Threading.CancellationToken"/>)
    /// before it could finish exploring every candidate interpretation. This
    /// is a distinct outcome from <see cref="Unresolved"/>: it does not mean
    /// no legal path exists, it means Foundgine cannot yet prove there is
    /// only one. A partial search is not evidence of a single meaning, so
    /// this outcome never carries a <see cref="GroundingDecision.Committed"/>
    /// interpretation — it fails closed rather than authorizing whatever
    /// was found before the limit was hit.</summary>
    BudgetExceeded
}

/// <summary>Which configured resource limit stopped a search that ended in
/// <see cref="GroundingOutcome.BudgetExceeded"/>. Exposed so the limit that
/// was hit can be logged, alerted on, or tuned per deployment without
/// parsing <see cref="GroundingDecision.Reason"/> text.</summary>
public enum GroundingBudgetLimit : byte
{
    /// <summary>No limit was hit; not applicable outside <see cref="GroundingOutcome.BudgetExceeded"/>.</summary>
    None,

    /// <summary>The expression tokenized to more tokens than <c>maxTokens</c> allows.
    /// Checked before any search begins, so this is the cheapest limit to hit.</summary>
    MaxTokens,

    /// <summary>The search visited more candidate/path nodes than <c>maxPathsExplored</c>
    /// allows before it could finish enumerating every interpretation. Backtracking is
    /// not a separately budgeted limit — every backtrack is a DFS re-entry, so it
    /// consumes this same shared work budget.</summary>
    MaxPathsExplored,

    /// <summary>The in-memory graph search ran longer than the configured <c>timeout</c>
    /// before it could finish enumerating every interpretation. This clock starts only
    /// after candidate retrieval has completed — see <see cref="RetrievalTimeout"/> for
    /// the stage before it.</summary>
    Timeout,

    /// <summary>Candidate retrieval for one or more tokens exceeded the configured
    /// <c>retrievalTimeout</c>. Retrieval happens entirely before the in-memory search
    /// budget starts counting, so this is a distinct limit from <see cref="Timeout"/> —
    /// it guards against a slow or hung candidate source (network partition, slow
    /// index), which none of the search-time bounds can see.</summary>
    RetrievalTimeout,

    /// <summary>The caller-supplied <see cref="System.Threading.CancellationToken"/>
    /// was cancelled before grounding finished, whether during retrieval or search.</summary>
    Cancelled
}

/// <summary>
/// One candidate meaning for a lexical expression: the token-by-token
/// mapping onto the semantic contract it commits to, its confidence, and
/// a signature that identifies what it means as distinct from how it got
/// there. Two interpretations that reach the same relationship, field, or
/// value via different bridging routes share a <see cref="Signature"/>;
/// two interpretations that map a token to a different field, value,
/// relationship, or root entity do not, even if their graph paths are
/// both legal.
/// </summary>
public sealed record GroundingInterpretation(
    IReadOnlyList<SemanticLexicalStep> Steps,
    double Confidence,
    EntityId RootEntity,
    string Signature)
{
    /// <summary>Lexical and graph-similarity evidence backing this interpretation,
    /// one group per step, in expression order. This is the evidence a person or
    /// an authorization log can inspect to see *why* this meaning was chosen —
    /// not just that a path existed.</summary>
    public IEnumerable<ResolutionEvidence> LexicalEvidence => Steps.SelectMany(x => x.Candidate.EffectiveEvidence);
}

/// <summary>
/// First-class record of how a lexical <param name="Expression"/> was grounded against the
/// frozen semantic contract: what it was committed to (if anything), what
/// else it could plausibly have meant, and whether that plurality of
/// meaning is material enough to block automatic commitment.
/// 
/// This exists because graph-constrained retrieval answers a narrower
/// question than grounding does. A candidate that fits the semantic graph
/// is not necessarily the meaning the user intended; a high retrieval
/// score is not meaning; a semantically valid path is not proof that it
/// is the intended path. <see>
///     <cref>SemanticLexicalResolver.Ground</cref>
/// </see>
/// surfaces every structurally valid, semantically distinct interpretation
/// instead of quietly returning the top-ranked one, so ambiguity can be
/// inspected, logged, or escalated to a clarifying question rather than
/// collapsed into a single authorized — and possibly wrong — execution.
/// </summary>
/// <param name="BudgetLimit">Which resource limit fired, when <param name="Outcome"/>
/// is <see cref="GroundingOutcome.BudgetExceeded"/>; <see cref="GroundingBudgetLimit.None"/>
/// otherwise.</param>
/// <param name="PartialInterpretationsAtCutoff">Populated only when <paramref name="Outcome"/>
/// is <see cref="GroundingOutcome.BudgetExceeded"/>: whatever semantically distinct
/// interpretations the search had already constructed at the moment the limit fired.
/// Diagnostic only — useful for logging, alerting, or deciding whether to raise a budget
/// and retry. A partial search is not proof of a unique legal meaning, so this list is
/// never treated as authorizable: <param name="Committed"/> stays null no matter how many
/// entries it has, and nothing in Foundgine executes against it <param name="CompetingInterpretations"/>
/// <param name="Reason"/><param name="RootCandidates"/>.</param>
public sealed record GroundingDecision(
    string Expression,
    GroundingOutcome Outcome,
    GroundingInterpretation? Committed,
    IReadOnlyList<GroundingInterpretation> CompetingInterpretations,
    string Reason,
    IReadOnlyList<SemanticLexicalCandidate> RootCandidates,
    GroundingBudgetLimit BudgetLimit = GroundingBudgetLimit.None,
    IReadOnlyList<GroundingInterpretation>? PartialInterpretationsAtCutoff = null)
{
    /// <summary>True when more than one semantically distinct interpretation was
    /// structurally legal, regardless of whether Foundgine still committed to one
    /// (because one interpretation dominated on confidence) or refused to
    /// (because two or more remained materially tied).</summary>
    public bool HadCompetingMeanings => CompetingInterpretations.Count > 0;

    /// <summary>Null-safe accessor for <see cref="PartialInterpretationsAtCutoff"/>.</summary>
    public IReadOnlyList<GroundingInterpretation> EffectivePartialInterpretationsAtCutoff =>
        PartialInterpretationsAtCutoff ?? [];
}
