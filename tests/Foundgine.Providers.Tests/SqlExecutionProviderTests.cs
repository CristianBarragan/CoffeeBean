using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Microsoft.Data.Sqlite;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers.Tests;

/// <summary>
/// These tests run SqlExecutionProvider against a real SQLite database (an
/// in-process, shared-cache, in-memory one — no file on disk, no external
/// server) rather than mocking ADO.NET. Proving "SQL provider consumes
/// QueryPlan / real DB executes query" from the 🔴 NOW checklist means
/// actually touching a database, not asserting against a fake reader.
/// </summary>
public sealed class SqlExecutionProviderTests : IAsyncLifetime
{
    // A named, shared-cache in-memory database: every connection opened with
    // this exact connection string during the test sees the same database,
    // as long as at least one connection (the "keeper" below) stays open.
    // Plain "Data Source=:memory:" would give each new connection (i.e.
    // every call SqlExecutionProvider makes) its own private, empty database.
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection _keeper = null!;

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var schema = _keeper.CreateCommand();
        schema.CommandText =
            """
            CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            CREATE TABLE Account (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Balance REAL NOT NULL);
            CREATE TABLE "Transaction" (Id INTEGER PRIMARY KEY, AccountId INTEGER NOT NULL, Amount REAL NOT NULL);

            INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (100, 10, -25.5);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (101, 10, 60.0);
            """;
        await schema.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _keeper.DisposeAsync();

    private static EntityMetadata Entity(ushort id, string name, params string[] columns) =>
        new(new EntityId(id), name,
            columns.Select((c, i) => new ColumnMetadata(new ColumnId((ushort)(i + 1)), c)).ToArray());

    private ExecutionContext Context() =>
        new(Guid.NewGuid(), new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

    [Fact]
    public async Task ExecuteAsync_SingleScan_ReturnsAllRows()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var plan = new ProviderPlan(new SqlScanNode(customer));
        var provider = new SqlExecutionProvider();

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(plan, Context()))
            rows.Add(row);

        var row0 = Assert.Single(rows);
        var values = row0.Entities[customer.EntityId.Value];
        Assert.Equal(1L, values[0]);
        Assert.Equal("Ada Lovelace", values[1]);
    }

    [Fact]
    public async Task ExecuteAsync_MissingConnectionString_Throws()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var plan = new ProviderPlan(new SqlScanNode(customer));
        var provider = new SqlExecutionProvider();
        var emptyContext = new ExecutionContext(Guid.NewGuid(), new Dictionary<string, object?>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in provider.ExecuteAsync(plan, emptyContext))
            {
            }
        });
    }

    [Fact]
    public async Task ExecuteAsync_CustomerAccountTransactionJoin_ReturnsJoinedRows_WithEachEntitysOwnColumns()
    {
        // The exact "🔴 NOW" scenario: Customer -> Account -> Transaction,
        // through a real ProviderPlan against a real database.
        var customer = Entity(1, "Customer", "Id", "Name");
        var account = Entity(2, "Account", "Id", "CustomerId", "Balance");
        var transaction = Entity(3, "Transaction", "Id", "AccountId", "Amount");

        var customerToAccount = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)), JoinKind.Inner);
        var accountToTransaction = new JoinMetadata(
            new JoinCondition(new ColumnReference(transaction, 2), new ColumnReference(account, 1)), JoinKind.Inner);

        var plan = new ProviderPlan(
            new SqlJoinNode(
                new SqlJoinNode(new SqlScanNode(customer), new SqlScanNode(account), customerToAccount),
                new SqlScanNode(transaction),
                accountToTransaction));

        var provider = new SqlExecutionProvider();
        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(plan, Context()))
            rows.Add(row);

        Assert.Equal(2, rows.Count); // two transactions on the one account

        foreach (var row in rows)
        {
            Assert.Equal("Ada Lovelace", row.Entities[customer.EntityId.Value][1]);
            Assert.Equal(500.0, row.Entities[account.EntityId.Value][2]);
            Assert.True(row.Entities.ContainsKey(transaction.EntityId.Value));
        }

        var amounts = rows.Select(r => (double)r.Entities[transaction.EntityId.Value][2]!).OrderBy(a => a).ToArray();
        Assert.Equal(new[] { -25.5, 60.0 }, amounts);
    }

    [Fact]
    public async Task ExecuteAsync_WithProjection_ReturnsOnlyTheProjectedColumns()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var fields = new[] { new FieldBinding(new ColumnReference(customer, 2), 1) };
        var plan = new ProviderPlan(new SqlProjectionNode(new SqlScanNode(customer), fields));
        var provider = new SqlExecutionProvider();

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(plan, Context()))
            rows.Add(row);

        var values = Assert.Single(rows).Entities[customer.EntityId.Value];
        Assert.Equal(2, values.Length); // sized to the entity's full column list...
        Assert.Null(values[0]);         // ...but only the projected column (index 1) is populated
        Assert.Equal("Ada Lovelace", values[1]);
    }
}
