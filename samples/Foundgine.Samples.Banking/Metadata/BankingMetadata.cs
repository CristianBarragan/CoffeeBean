using Foundgine.Metadata;

namespace Foundgine.Samples.Banking.Metadata;

/// <summary>
/// Hand-written Foundgine.Metadata for <see cref="Domain.Customer"/> and
/// <see cref="Domain.Account"/>. In Graphgine, code shaped like this is
/// emitted by Graphgine.SourceGenerators from a GraphQL-oriented mapping
/// class; here it's written by hand to make the point that
/// Foundgine.Metadata itself has no idea GraphQL — or Graphgine — exists.
/// A second, non-GraphQL product built on Foundgine would either write
/// metadata like this directly, or bring its own generator that targets
/// these same record types.
/// </summary>
public static class BankingMetadata
{
    public static readonly EntityMetadata Customer = new(
        new EntityId(1),
        "Customer",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "Name"),
        });

    public static readonly EntityMetadata Account = new(
        new EntityId(2),
        "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "Balance"),
        });

    /// <summary>Account.CustomerId = Customer.Id</summary>
    public static readonly JoinMetadata AccountToCustomer = new(
        new JoinCondition(
            Left: new ColumnReference(Account, ColumnId: 2),
            Right: new ColumnReference(Customer, ColumnId: 1)),
        JoinKind.Inner);
}
