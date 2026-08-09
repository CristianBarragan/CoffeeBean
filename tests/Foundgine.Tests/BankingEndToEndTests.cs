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
/// The whole point of the minimal active tree: prove
///
///     Domain -> Metadata -> Dynamic Planner -> QueryPlan
///            -> ProviderPlan -> SQL -> real database -> Result
///
/// for Customer -> Account -> Transaction, in one test that exercises every
/// project in the five-project spine (Metadata, Builders, Planning,
/// Execution.Contracts, Providers) with no step faked or skipped. This is
/// the "E2E test passes" line item on the root README's 🔴 NOW checklist.
/// </summary>
public sealed class BankingEndToEndTests : IAsyncLifetime
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

    private static readonly EntityMetadata Account = new(
        new EntityId(2),
        "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "Balance")
        });

    private static readonly EntityMetadata TransactionEntity = new(
        new EntityId(3),
        "Transaction",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "AccountId"),
            new(new ColumnId(3), "Amount")
        });

    private static readonly JoinMetadata AccountToCustomer = new(
        new JoinCondition(
            new ColumnReference(Account, 2),
            new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    private static readonly JoinMetadata AccountToTransaction = new(
        new JoinCondition(
            new ColumnReference(TransactionEntity, 2),
            new ColumnReference(Account, 1)),
        JoinKind.Inner);

    // A second child of Customer, unrelated to Account/Transaction, so the
    // branching test below has real fan-out to plan against a real
    // database: Customer -> Accounts -> Transactions *and*
    // Customer -> ContactPoints.
    private static readonly EntityMetadata ContactPoint = new(
        new EntityId(4),
        "ContactPoint",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "Kind")
        });

    private static readonly JoinMetadata CustomerToContactPoint = new(
        new JoinCondition(
            new ColumnReference(ContactPoint, 2),
            new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            CREATE TABLE Account (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Balance REAL NOT NULL);
            CREATE TABLE "Transaction" (Id INTEGER PRIMARY KEY, AccountId INTEGER NOT NULL, Amount REAL NOT NULL);
            CREATE TABLE ContactPoint (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Kind TEXT NOT NULL);

            INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
            INSERT INTO Customer (Id, Name) VALUES (2, 'Grace Hopper');
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (20, 2, 1000.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (100, 10, -25.5);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (101, 10, 60.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (200, 20, 10.0);
            INSERT INTO ContactPoint (Id, CustomerId, Kind) VALUES (1000, 1, 'Email');
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
    public async Task CustomerAccountTransaction_PlansCompilesAndExecutes_AgainstARealDatabase()
    {
        // 1) Domain -> Metadata: a MetadataRegistry + JoinGraph, exactly as
        //    a real application would build once at startup.
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(Account);
        registry.Register(TransactionEntity);

        var joinGraph = new JoinGraph();
        joinGraph.AddEdge(
            Customer.EntityId,
            Account.EntityId,
            AccountToCustomer);

        joinGraph.AddEdge(
            Account.EntityId,
            TransactionEntity.EntityId,
            AccountToTransaction);

        // 2) Metadata + Intent -> QueryPlan, via the dynamic planner. Note
        //    there is no Banking-specific code between here and
        //    Foundgine.Planning — the planner discovers both joins from
        //    joinGraph.
        var planner = new QueryPlanner(registry, joinGraph);

        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: new[]
            {
                Account.EntityId,
                TransactionEntity.EntityId
            });

        var queryPlan = planner.Plan(intent);

        // TECH-DEBT-001: the logical plan preserves QueryIntent's tree
        // shape as a CompositeNode — it's SqlPlanCompiler, not
        // QueryPlanner, that flattens it into a relational join chain.
        Assert.IsType<CompositeNode>(queryPlan.Root);

        // 3) QueryPlan -> ProviderPlan, via the SQL compiler.
        var providerPlan = SqlPlanCompiler.Compile(queryPlan);

        Assert.IsType<SqlJoinNode>(providerPlan.Root);

        // 4) ProviderPlan -> SQL -> real database -> Result.
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

        // Three transactions total across both customers' accounts.
        Assert.Equal(3, rows.Count);

        var adasRows = rows
            .Where(r =>
                (string)Occurrence(r, Customer.EntityId).Values[1]! ==
                "Ada Lovelace")
            .ToArray();

        Assert.Equal(2, adasRows.Length);

        Assert.All(
            adasRows,
            r => Assert.Equal(
                10L,
                Occurrence(r, Account.EntityId).Values[0]));

        var graceRows = rows
            .Where(r =>
                (string)Occurrence(r, Customer.EntityId).Values[1]! ==
                "Grace Hopper")
            .ToArray();

        var graceRow = Assert.Single(graceRows);

        Assert.Equal(
            20L,
            Occurrence(graceRow, Account.EntityId).Values[0]);

        Assert.Equal(
            10.0,
            Occurrence(graceRow, TransactionEntity.EntityId).Values[2]);
    }

    [Fact]
    public async Task CustomerToTransaction_WithoutGoingThroughAccount_IsRejectedByThePlanner()
    {
        // The planner must not invent a relationship that metadata never
        // described, even though a human could see "obviously" Transaction
        // relates to Customer transitively through Account.
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(TransactionEntity);

        var joinGraph = new JoinGraph();

        // No edges registered at all.
        var planner = new QueryPlanner(registry, joinGraph);

        Assert.Throws<InvalidOperationException>(() =>
            planner.Plan(
                QueryIntent.Linear(
                    Customer.EntityId,
                    new[] { TransactionEntity.EntityId })));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task CustomerToAccountsToTransactionsAndContactPoints_BranchingIntent_PlansCompilesAndExecutes_AgainstARealDatabase()
    {
        // FOUND-002: the same pipeline as the linear E2E above, but with a
        // branching QueryIntent —
        //
        //   Customer
        //   ├── Accounts
        //   │    └── Transactions
        //   └── ContactPoints
        //
        // — proving the planner produces a tree, the SQL compiler and text
        // translator handle it with no special-casing, and it executes
        // correctly against a real database.
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(Account);
        registry.Register(TransactionEntity);
        registry.Register(ContactPoint);

        var joinGraph = new JoinGraph();

        joinGraph.AddEdge(
            Customer.EntityId,
            Account.EntityId,
            AccountToCustomer);

        joinGraph.AddEdge(
            Account.EntityId,
            TransactionEntity.EntityId,
            AccountToTransaction);

        joinGraph.AddEdge(
            Customer.EntityId,
            ContactPoint.EntityId,
            CustomerToContactPoint);

        var planner = new QueryPlanner(registry, joinGraph);

        var intent = new QueryIntent(
            Root: Customer.EntityId,
            Branches: new[]
            {
                new QueryIntentBranch(
                    Account.EntityId,
                    new[]
                    {
                        new QueryIntentBranch(
                            TransactionEntity.EntityId)
                    }),

                new QueryIntentBranch(
                    ContactPoint.EntityId)
            });

        var queryPlan = planner.Plan(intent);
        var providerPlan = SqlPlanCompiler.Compile(queryPlan);

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

        // Ada has 1 account combined with her 2 transactions and her
        // 1 contact point, producing 2 rows (2 x 1).
        //
        // Grace has 1 transaction but no contact point, so the inner join
        // drops her entirely — exactly the SQL semantics a hand-written
        // query would have.
        Assert.Equal(2, rows.Count);

        Assert.All(rows, row =>
        {
            Assert.Equal(
                "Ada Lovelace",
                (string)Occurrence(row, Customer.EntityId).Values[1]!);

            Assert.Equal(
                "Email",
                (string)Occurrence(row, ContactPoint.EntityId).Values[2]!);
        });
    }
}