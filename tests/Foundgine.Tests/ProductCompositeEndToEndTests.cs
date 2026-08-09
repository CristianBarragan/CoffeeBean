using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Providers;
using Microsoft.Data.Sqlite;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Tests;

/// <summary>
/// FOUND-003: the "genuinely hard composite query" milestone. Where
/// <see cref="BankingEndToEndTests"/> proves the five-project spine on a
/// three-entity chain (Customer -> Account -> Transaction), this proves it
/// on the domain's real depth — the logical <c>Product</c> a customer
/// holds is not one storage entity, it's a *view* across five of them:
///
///   Product
///   ├── Customer
///   ├── CustomerBankingRelationship
///   ├── Contract
///   ├── Account
///   └── Transaction
///
/// backed by the physical chain
///
///   Customer -> CustomerBankingRelationship -> Contract -> Account -> Transaction
///
/// There is no <c>Product</c> table and no <c>Product</c> EntityMetadata:
/// nothing here changes about how Foundgine.Planning/Foundgine.Providers
/// work. That's the point being tested — "Product" is just what you get
/// when a caller expresses a five-entity QueryIntent and lets
/// QueryPlanner/SqlPlanCompiler/SqlExecutionProvider assemble it out of
/// entities that already exist, exactly the same way the three-entity
/// Banking E2E does. No new capability is exercised, only more of the
/// existing one at once.
/// </summary>
public sealed class ProductCompositeEndToEndTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection _keeper = null!;

    private static readonly EntityMetadata Customer = new(
        new EntityId(1),
        "Customer",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "Name")
        });

    private static readonly EntityMetadata CustomerBankingRelationship = new(
        new EntityId(2),
        "CustomerBankingRelationship",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "RelationshipType")
        });

    private static readonly EntityMetadata Contract = new(
        new EntityId(3),
        "Contract",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerBankingRelationshipId"),
            new(new ColumnId(3), "ContractType"),
            new(new ColumnId(4), "Amount")
        });

    private static readonly EntityMetadata Account = new(
        new EntityId(4),
        "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "ContractId"),
            new(new ColumnId(3), "AccountNumber"),
            new(new ColumnId(4), "Balance")
        });

    private static readonly EntityMetadata TransactionEntity = new(
        new EntityId(5),
        "Transaction",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "AccountId"),
            new(new ColumnId(3), "Amount")
        });

    /// <summary>
    /// CustomerBankingRelationship.CustomerId = Customer.Id
    /// </summary>
    private static readonly JoinMetadata RelationshipToCustomer = new(
        new JoinCondition(
            new ColumnReference(CustomerBankingRelationship, 2),
            new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    /// <summary>
    /// Contract.CustomerBankingRelationshipId =
    /// CustomerBankingRelationship.Id
    /// </summary>
    private static readonly JoinMetadata ContractToRelationship = new(
        new JoinCondition(
            new ColumnReference(Contract, 2),
            new ColumnReference(CustomerBankingRelationship, 1)),
        JoinKind.Inner);

    /// <summary>
    /// Account.ContractId = Contract.Id
    /// </summary>
    private static readonly JoinMetadata AccountToContract = new(
        new JoinCondition(
            new ColumnReference(Account, 2),
            new ColumnReference(Contract, 1)),
        JoinKind.Inner);

    /// <summary>
    /// Transaction.AccountId = Account.Id
    /// </summary>
    private static readonly JoinMetadata TransactionToAccount = new(
        new JoinCondition(
            new ColumnReference(TransactionEntity, 2),
            new ColumnReference(Account, 1)),
        JoinKind.Inner);

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            CREATE TABLE CustomerBankingRelationship (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, RelationshipType TEXT NOT NULL);
            CREATE TABLE Contract (Id INTEGER PRIMARY KEY, CustomerBankingRelationshipId INTEGER NOT NULL, ContractType TEXT NOT NULL, Amount REAL NOT NULL);
            CREATE TABLE Account (Id INTEGER PRIMARY KEY, ContractId INTEGER NOT NULL, AccountNumber TEXT NOT NULL, Balance REAL NOT NULL);
            CREATE TABLE "Transaction" (Id INTEGER PRIMARY KEY, AccountId INTEGER NOT NULL, Amount REAL NOT NULL);

            -- Ada: one banking relationship, one mortgage contract, one
            -- account, two transactions.
            INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
            INSERT INTO CustomerBankingRelationship (Id, CustomerId, RelationshipType) VALUES (10, 1, 'Primary');
            INSERT INTO Contract (Id, CustomerBankingRelationshipId, ContractType, Amount) VALUES (100, 10, 'Mortgage', 250000.0);
            INSERT INTO Account (Id, ContractId, AccountNumber, Balance) VALUES (1000, 100, 'ACC-1000', 500.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10000, 1000, -25.5);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10001, 1000, 60.0);

            -- Grace: one banking relationship, one credit-card contract,
            -- one account, one transaction.
            INSERT INTO Customer (Id, Name) VALUES (2, 'Grace Hopper');
            INSERT INTO CustomerBankingRelationship (Id, CustomerId, RelationshipType) VALUES (20, 2, 'Primary');
            INSERT INTO Contract (Id, CustomerBankingRelationshipId, ContractType, Amount) VALUES (200, 20, 'CreditCard', 5000.0);
            INSERT INTO Account (Id, ContractId, AccountNumber, Balance) VALUES (2000, 200, 'ACC-2000', 1000.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (20000, 2000, 10.0);
            """;

        await setup.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() =>
        await _keeper.DisposeAsync();

    private static EntityOccurrence Occurrence(
        ExecutionRow row,
        EntityId entityId,
        int occurrenceIndex = 0)
    {
        return Assert.Single(row.Occurrences, x =>
                x.EntityId == entityId &&
                x.OccurrenceIndex == occurrenceIndex);
    }

    [Fact]
    public async Task Product_AsFiveEntityComposite_PlansCompilesAndExecutes_AgainstARealDatabase()
    {
        // 1) Domain -> Metadata. "Product" never appears here: only the
        //    five real storage entities and the joins between them, built
        //    once exactly as a real application would at startup.
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(CustomerBankingRelationship);
        registry.Register(Contract);
        registry.Register(Account);
        registry.Register(TransactionEntity);

        var joinGraph = new JoinGraph();

        joinGraph.AddEdge(
            Customer.EntityId,
            CustomerBankingRelationship.EntityId,
            RelationshipToCustomer);

        joinGraph.AddEdge(
            CustomerBankingRelationship.EntityId,
            Contract.EntityId,
            ContractToRelationship);

        joinGraph.AddEdge(
            Contract.EntityId,
            Account.EntityId,
            AccountToContract);

        joinGraph.AddEdge(
            Account.EntityId,
            TransactionEntity.EntityId,
            TransactionToAccount);

        // 2) Metadata + Intent -> QueryPlan. The "Product" shape is
        //    entirely expressed here, as a five-deep linear QueryIntent —
        //    the planner discovers all four joins from joinGraph, the same
        //    way it discovers two joins in the Banking E2E.
        var planner = new QueryPlanner(registry, joinGraph);

        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: new[]
            {
                CustomerBankingRelationship.EntityId,
                Contract.EntityId,
                Account.EntityId,
                TransactionEntity.EntityId
            });

        var queryPlan = planner.Plan(intent);

        Assert.IsType<CompositeNode>(queryPlan.Root);

        // 3) QueryPlan -> ProviderPlan: still a single flattened
        //    SqlJoinNode chain, now four joins deep instead of two.
        var providerPlan = SqlPlanCompiler.Compile(queryPlan);

        Assert.IsType<SqlJoinNode>(providerPlan.Root);

        // 4) ProviderPlan -> SQL -> real database -> Product-shaped result.
        var provider = new SqlExecutionProvider();

        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?>
            {
                ["ConnectionString"] = _connectionString
            });

        var rows = new List<ExecutionRow>();

        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        // Three transactions total, exactly as in the three-entity Banking
        // E2E — but this time every row also carries the
        // CustomerBankingRelationship and Contract that sit between
        // Customer and Account, proving the deeper chain joins correctly
        // end to end rather than just "doesn't throw".
        Assert.Equal(3, rows.Count);

        var adaRows = rows
            .Where(r =>
                (string)Occurrence(r, Customer.EntityId).Values[1]! ==
                "Ada Lovelace")
            .ToArray();

        Assert.Equal(2, adaRows.Length);

        Assert.All(adaRows, r =>
        {
            Assert.Equal(
                "Primary",
                (string)Occurrence(
                    r,
                    CustomerBankingRelationship.EntityId).Values[2]!);

            Assert.Equal(
                "Mortgage",
                (string)Occurrence(
                    r,
                    Contract.EntityId).Values[2]!);

            Assert.Equal(
                250000.0,
                (double)Occurrence(
                    r,
                    Contract.EntityId).Values[3]!);

            Assert.Equal(
                "ACC-1000",
                (string)Occurrence(
                    r,
                    Account.EntityId).Values[2]!);
        });

        var graceRow = Assert.Single(rows, r =>
                (string)Occurrence(
                    r,
                    Customer.EntityId).Values[1]! ==
                "Grace Hopper");

        Assert.Equal(
            "CreditCard",
            (string)Occurrence(
                graceRow,
                Contract.EntityId).Values[2]!);

        Assert.Equal(
            "ACC-2000",
            (string)Occurrence(
                graceRow,
                Account.EntityId).Values[2]!);

        Assert.Equal(
            10.0,
            (double)Occurrence(
                graceRow,
                TransactionEntity.EntityId).Values[2]!);
    }

    [Fact]
    public async Task Customer_ToContract_SkippingCustomerBankingRelationship_IsRejectedByThePlanner()
    {
        // The composite "Product" view only exists because the planner
        // walks a real chain of registered joins. It must not shortcut
        // Customer straight to Contract just because a human reading the
        // diagram can see they're transitively related through
        // CustomerBankingRelationship.
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(Contract);

        var joinGraph = new JoinGraph();

        // No CustomerBankingRelationship edge registered.
        var planner = new QueryPlanner(registry, joinGraph);

        Assert.Throws<InvalidOperationException>(() =>
            planner.Plan(
                QueryIntent.Linear(
                    Customer.EntityId,
                    new[] { Contract.EntityId })));

        await Task.CompletedTask;
    }
}