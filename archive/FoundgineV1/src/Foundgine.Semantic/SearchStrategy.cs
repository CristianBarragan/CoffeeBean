namespace Foundgine.Semantic;

/// <summary>
/// How a <see cref="SearchCapability"/>'s fields should be matched against
/// ambiguous human language. Milestone 2's resolver reads this to decide
/// how much confidence an exact vs. fuzzy match deserves; it never invents
/// a matching strategy on its own.
/// </summary>
public enum SearchStrategy
{
    Exact,
    Prefix,
    Fuzzy
}
