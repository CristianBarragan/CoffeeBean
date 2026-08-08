namespace Foundgine.Samples.Banking.Domain;

public sealed class Account
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public decimal Balance { get; init; }
}
