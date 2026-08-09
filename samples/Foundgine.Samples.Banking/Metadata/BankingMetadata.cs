using Foundgine.Metadata;

namespace Foundgine.Samples.Banking.Metadata;

/// <summary>
/// Hand-written Foundgine.Metadata for Customer -> Account -> Transaction.
/// In Graphgine, code shaped like this is emitted by
/// Graphgine.SourceGenerators from a GraphQL-oriented mapping class; here
/// it's written by hand to make the point that Foundgine.Metadata itself
/// has no idea GraphQL — or Graphgine — exists. A second, non-GraphQL
/// product built on Foundgine would either write metadata like this
/// directly, or bring its own generator that targets these same record
/// types.
///
/// <see cref="Registry"/> and <see cref="Joins"/> are what
/// Foundgine.Planning.QueryPlanner actually consumes — the individual
/// EntityMetadata/JoinMetadata fields below exist mainly so this sample can
/// also build a ProviderPlan by hand for comparison (see Program.cs).
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

    public static readonly EntityMetadata Transaction = new(
        new EntityId(3),
        "Transaction",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "AccountId"),
            new(new ColumnId(3), "Amount"),
        });

    /// <summary>Account.CustomerId = Customer.Id</summary>
    public static readonly JoinMetadata AccountToCustomer = new(
        new JoinCondition(
            Left: new ColumnReference(Account, ColumnId: 2),
            Right: new ColumnReference(Customer, ColumnId: 1)),
        JoinKind.Inner);

    /// <summary>Transaction.AccountId = Account.Id</summary>
    public static readonly JoinMetadata AccountToTransaction = new(
        new JoinCondition(
            Left: new ColumnReference(Transaction, ColumnId: 2),
            Right: new ColumnReference(Account, ColumnId: 1)),
        JoinKind.Inner);

    /// <summary>
    /// Everything Foundgine.Planning.QueryPlanner needs to reason about this
    /// domain dynamically, instead of any code hardcoding "Customer joins to
    /// Account joins to Transaction".
    /// </summary>
    public static MetadataRegistry Registry
    {
        get
        {
            var registry = new MetadataRegistry();
            registry.Register(Customer);
            registry.Register(Account);
            registry.Register(Transaction);
            return registry;
        }
    }

    public static JoinGraph Joins
    {
        get
        {
            var joins = new JoinGraph();
            joins.AddEdge(Customer.EntityId, Account.EntityId, AccountToCustomer);
            joins.AddEdge(Account.EntityId, Transaction.EntityId, AccountToTransaction);
            return joins;
        }
    }
}
