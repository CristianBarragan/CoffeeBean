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
/// The architecture review's "Step 1 — make the physical schema ugly"
/// checkpoint: run the exact same branching intent as
/// <see cref="BankingEndToEndTests"/> —
///
///   Customer
///   ├── Accounts
///   │    └── Transactions
///   └── ContactPoints
///
/// — but against a physical schema whose table and column names have
/// nothing in common with the domain names. <see cref="QueryIntent"/>,
/// <see cref="QueryPlanner"/>, and <see cref="JoinGraph"/> only ever see
/// <c>Customer</c>/<c>Account</c>/<c>Transaction</c>/<c>ContactPoint</c>
/// and their domain column names; only <see cref="EntityMetadata.StorageName"/>
/// and <see cref="ColumnMetadata.StorageName"/> — read solely by
/// <see cref="SqlTextTranslator"/> — know the real table is
/// <c>crm_customer</c>, not <c>Customer</c>.
///
/// If this test passes, the planner genuinely doesn't "peek" at physical
/// names anywhere — it can't, since it never received them.
/// </summary>
public sealed class UglySchemaEndToEndTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection _keeper = null!;

    // Domain names (what QueryIntent/JoinGraph/QueryPlanner reason about)
    // vs. StorageName (what actually exists in the database). Deliberately
    // as unrelated as the architecture review's example schema:
    //   Customer -> crm_customer, Account -> acct_master,
    //   Transaction -> txn_header, ContactPoint -> contact_point_record.
    private static readonly EntityMetadata Customer = new(
        new EntityId(1), "Customer",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id", StorageName: "customer_pk"),
            new(new ColumnId(2), "Name", StorageName: "full_name"),
        },
        StorageName: "crm_customer");

    private static readonly EntityMetadata Account = new(
        new EntityId(2), "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id", StorageName: "account_pk"),
            new(new ColumnId(2), "CustomerId", StorageName: "owner_customer_id"),
            new(new ColumnId(3), "Balance", StorageName: "current_balance"),
        },
        StorageName: "acct_master");

    private static readonly EntityMetadata TransactionEntity = new(
        new EntityId(3), "Transaction",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id", StorageName: "txn_pk"),
            new(new ColumnId(2), "AccountId", StorageName: "owning_account_id"),
            new(new ColumnId(3), "Amount", StorageName: "txn_amount"),
        },
        StorageName: "txn_header");

    private static readonly EntityMetadata ContactPoint = new(
        new EntityId(4), "ContactPoint",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id", StorageName: "contact_pk"),
            new(new ColumnId(2), "CustomerId", StorageName: "linked_customer_id"),
            new(new ColumnId(3), "Kind", StorageName: "contact_kind"),
        },
        StorageName: "contact_point_record");

    // Join conditions still reference domain ColumnIds (2, 1, ...) exactly
    // as BankingEndToEndTests does — the physical rename is invisible here
    // too. SqlTextTranslator resolves the physical column at SQL-generation
    // time, not the JoinGraph.
    private static readonly JoinMetadata AccountToCustomer = new(
        new JoinCondition(new ColumnReference(Account, 2), new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    private static readonly JoinMetadata AccountToTransaction = new(
        new JoinCondition(new ColumnReference(TransactionEntity, 2), new ColumnReference(Account, 1)),
        JoinKind.Inner);

    private static readonly JoinMetadata CustomerToContactPoint = new(
        new JoinCondition(new ColumnReference(ContactPoint, 2), new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        // Every identifier here is the *physical* name. No domain name
        // (Customer, Account, Transaction, ContactPoint, Id, Name, ...)
        // appears anywhere in this schema on purpose.
        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE crm_customer (customer_pk INTEGER PRIMARY KEY, full_name TEXT NOT NULL);
            CREATE TABLE acct_master (account_pk INTEGER PRIMARY KEY, owner_customer_id INTEGER NOT NULL, current_balance REAL NOT NULL);
            CREATE TABLE txn_header (txn_pk INTEGER PRIMARY KEY, owning_account_id INTEGER NOT NULL, txn_amount REAL NOT NULL);
            CREATE TABLE contact_point_record (contact_pk INTEGER PRIMARY KEY, linked_customer_id INTEGER NOT NULL, contact_kind TEXT NOT NULL);

            INSERT INTO crm_customer (customer_pk, full_name) VALUES (1, 'Ada Lovelace');
            INSERT INTO crm_customer (customer_pk, full_name) VALUES (2, 'Grace Hopper');
            INSERT INTO acct_master (account_pk, owner_customer_id, current_balance) VALUES (10, 1, 500.0);
            INSERT INTO acct_master (account_pk, owner_customer_id, current_balance) VALUES (20, 2, 1000.0);
            INSERT INTO txn_header (txn_pk, owning_account_id, txn_amount) VALUES (100, 10, -25.5);
            INSERT INTO txn_header (txn_pk, owning_account_id, txn_amount) VALUES (101, 10, 60.0);
            INSERT INTO txn_header (txn_pk, owning_account_id, txn_amount) VALUES (200, 20, 10.0);
            INSERT INTO contact_point_record (contact_pk, linked_customer_id, contact_kind) VALUES (1000, 1, 'Email');
            """;
        await setup.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _keeper.DisposeAsync();

    [Fact]
    public async Task BranchingIntent_OverRenamedPhysicalSchema_PlansCompilesAndExecutes_WithNoPhysicalNamesInTheIntent()
    {
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(Account);
        registry.Register(TransactionEntity);
        registry.Register(ContactPoint);

        var joinGraph = new JoinGraph();
        joinGraph.AddEdge(Customer.EntityId, Account.EntityId, AccountToCustomer);
        joinGraph.AddEdge(Account.EntityId, TransactionEntity.EntityId, AccountToTransaction);
        joinGraph.AddEdge(Customer.EntityId, ContactPoint.EntityId, CustomerToContactPoint);

        // The intent itself is character-for-character identical in shape
        // to BankingEndToEndTests' branching intent — same domain EntityIds,
        // same tree — because from QueryIntent's point of view nothing
        // about the physical schema changed.
        var planner = new QueryPlanner(registry, joinGraph);
        var intent = new QueryIntent(
            Root: Customer.EntityId,
            Branches: new[]
            {
                new QueryIntentBranch(
                    Account.EntityId,
                    new[] { new QueryIntentBranch(TransactionEntity.EntityId) }),
                new QueryIntentBranch(ContactPoint.EntityId),
            });

        var queryPlan = planner.Plan(intent);
        var providerPlan = SqlPlanCompiler.Compile(queryPlan);

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        // Same expected shape as the equivalent BankingEndToEndTests case:
        // Ada's 2 transactions x her 1 contact point = 2 rows; Grace has no
        // contact point, so the inner join drops her.
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("Ada Lovelace", (string)r.Entities[Customer.EntityId.Value][1]!);
            Assert.Equal("Email", (string)r.Entities[ContactPoint.EntityId.Value][2]!);
        });
    }
}
