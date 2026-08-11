namespace Foundgine.Semantic.Resolution;

/// <summary>
/// One row an <see cref="ICandidateSource"/> found that could be the
/// answer to a resolution query -- its identity value plus a
/// human-readable label for evidence and ambiguity messages (e.g. identity
/// "1", label "Ada Lovelace"). Deliberately just two strings:
/// <see cref="EntityResolver"/> reasons about candidates generically and
/// never needs to know their CLR type.
/// </summary>
public sealed record IdentityCandidate(string IdentityValue, string DisplayLabel);
