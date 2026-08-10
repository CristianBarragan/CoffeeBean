namespace Foundgine.Samples.Banking.Domain;

/// <summary>
/// Plain domain type — no GraphQL mapping attributes, no base class, no
/// interface. Graphgine's source generator reads mapping classes shaped
/// around GraphQL concerns to produce this kind of metadata automatically;
/// a pure-Foundgine consumer with no generator of its own just describes it
/// by hand, as <see cref="Metadata.BankingMetadata"/> does.
/// </summary>
public sealed class Customer
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
