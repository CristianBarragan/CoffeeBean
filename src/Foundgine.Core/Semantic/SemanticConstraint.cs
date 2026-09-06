namespace Foundgine.Core.Semantic;

/// <summary>Provider-neutral semantic constraint attached to a field.</summary>
public sealed record SemanticConstraint(
    SemanticConstraintKind Kind,
    string? Value = null,
    decimal? Minimum = null,
    decimal? Maximum = null)
{
    public static SemanticConstraint Range(decimal? minimum = null, decimal? maximum = null)
    {
        return new(SemanticConstraintKind.Range, Minimum: minimum, Maximum: maximum);
    }

    public static SemanticConstraint Pattern(string pattern)
    {
        return new SemanticConstraint(SemanticConstraintKind.Pattern, pattern);
    }

    public static SemanticConstraint Temporal(string semantics)
    {
        return new SemanticConstraint(SemanticConstraintKind.Temporal, semantics);
    }

    public static SemanticConstraint Currency(string currencyCode)
    {
        return new SemanticConstraint(SemanticConstraintKind.Currency, currencyCode);
    }

    public static SemanticConstraint CountryCode(string countryCode)
    {
        return new SemanticConstraint(SemanticConstraintKind.CountryCode, countryCode);
    }
}

public enum SemanticConstraintKind : byte
{
    Range,
    Pattern,
    Temporal,
    Currency,
    CountryCode
}