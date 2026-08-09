namespace Foundgine.Samples.Banking.Domain;

public sealed class Transaction
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public decimal Amount { get; init; }
}
