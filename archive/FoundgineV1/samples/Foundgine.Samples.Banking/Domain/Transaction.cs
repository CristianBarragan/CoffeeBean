namespace Foundgine.Samples.Banking.Domain;

public sealed class Transaction
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public decimal Amount { get; init; }

    /// <summary>
    /// Added for Milestone 4: without a real point in time on each
    /// transaction, "Ada's last five transactions" has no honest meaning
    /// — Id order is an implementation detail, not "last". This is what
    /// the sample's ORDER BY TransactionDate DESC actually sorts on.
    /// </summary>
    public DateTime TransactionDate { get; init; }
}
