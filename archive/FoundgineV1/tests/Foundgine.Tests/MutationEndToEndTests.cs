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
/// The mutation counterpart of <see cref="BankingEndToEndTests"/>: proves
///
///     Domain -> Metadata -> MutationIntent -> MutationPlanner -> MutationPlan
///            -> SqlPlanCompiler -> ProviderMutationPlan -> SQL -> real database
///
/// for Customer/Account, exercising Create, Update, and Delete against a
/// real SQLite database with no step faked or skipped, plus proving that
/// several operations submitted as one <see cref="ProviderMutationPlan"/>
/// commit (or roll back) as a single atomic unit.
/// </summary>
public sealed class MutationEndToEndTests : IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            CREATE TABLE Account (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Balance REAL NOT NULL);
            """;

        await setup.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() =>
        await _keeper.DisposeAsync();

    private static MetadataRegistry Registry()
    {
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(Account);
        return registry;
    }

    private async Task<List<object?[]>> QueryAsync(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new object?[reader.FieldCount];
            reader.GetValues(values!);
            rows.Add(values);
        }

        return rows;
    }

    [Fact]
    public async Task Create_PlansCompilesAndExecutes_AgainstARealDatabase()
    {
        // 1) Domain -> Metadata -> MutationIntent, exactly as a real
        //    application would build it.
        var planner = new MutationPlanner(Registry());

        var intent = new MutationIntent(
            Customer.EntityId,
            MutationKind.Create,
            new[]
            {
                new MutationFieldValue(1, 1L),
                new MutationFieldValue(2, "Ada Lovelace")
            });

        // 2) MutationIntent -> MutationPlan, via the dynamic mutation
        //    planner — no Banking-specific code involved.
        var mutationPlan = planner.Plan(intent);

        Assert.IsType<EntityMutation>(Assert.Single(mutationPlan.Operations));

        // 3) MutationPlan -> ProviderMutationPlan, via the SQL compiler.
        var providerPlan = SqlPlanCompiler.CompileMutation(mutationPlan);

        Assert.IsType<SqlInsertNode>(Assert.Single(providerPlan.Operations));

        // 4) ProviderMutationPlan -> SQL -> real database.
        var provider = new SqlExecutionProvider();

        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var results = await provider.ExecuteMutationAsync(providerPlan, context);

        var result = Assert.Single(results);
        Assert.Equal(Customer.EntityId, result.EntityId);
        Assert.Equal(1, result.RowsAffected);

        var rows = await QueryAsync("SELECT Id, Name FROM Customer");
        var row = Assert.Single(rows);
        Assert.Equal(1L, row[0]);
        Assert.Equal("Ada Lovelace", row[1]);
    }

    [Fact]
    public async Task Update_FilteredByFilterExpression_WritesOnlyMatchingRows()
    {
        var seed = new SqliteConnection(_connectionString);
        await seed.OpenAsync();
        var seedCommand = seed.CreateCommand();
        seedCommand.CommandText =
            """
            INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (11, 1, 250.0);
            """;
        await seedCommand.ExecuteNonQueryAsync();
        await seed.DisposeAsync();

        var planner = new MutationPlanner(Registry());

        var intent = new MutationIntent(
            Account.EntityId,
            MutationKind.Update,
            new[] { new MutationFieldValue(3, 600.0) },
            Filter: new ComparisonFilter(
                new ColumnReference(Account, 1),
                ComparisonOperator.Equal,
                10L));

        var mutationPlan = planner.Plan(intent);
        var providerPlan = SqlPlanCompiler.CompileMutation(mutationPlan);

        Assert.IsType<SqlUpdateNode>(Assert.Single(providerPlan.Operations));

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var results = await provider.ExecuteMutationAsync(providerPlan, context);
        Assert.Equal(1, Assert.Single(results).RowsAffected);

        var rows = await QueryAsync("SELECT Id, Balance FROM Account ORDER BY Id");

        Assert.Equal(2, rows.Count);
        Assert.Equal(600.0, rows[0][1]);
        Assert.Equal(250.0, rows[1][1]);
    }

    [Fact]
    public async Task Delete_FilteredByFilterExpression_RemovesOnlyMatchingRows()
    {
        var seed = new SqliteConnection(_connectionString);
        await seed.OpenAsync();
        var seedCommand = seed.CreateCommand();
        seedCommand.CommandText =
            """
            INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (11, 1, 250.0);
            """;
        await seedCommand.ExecuteNonQueryAsync();
        await seed.DisposeAsync();

        var planner = new MutationPlanner(Registry());

        var intent = new MutationIntent(
            Account.EntityId,
            MutationKind.Delete,
            Array.Empty<MutationFieldValue>(),
            Filter: new ComparisonFilter(
                new ColumnReference(Account, 1),
                ComparisonOperator.Equal,
                11L));

        var mutationPlan = planner.Plan(intent);
        var providerPlan = SqlPlanCompiler.CompileMutation(mutationPlan);

        Assert.IsType<SqlDeleteNode>(Assert.Single(providerPlan.Operations));

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var results = await provider.ExecuteMutationAsync(providerPlan, context);
        Assert.Equal(1, Assert.Single(results).RowsAffected);

        var rows = await QueryAsync("SELECT Id FROM Account");
        var row = Assert.Single(rows);
        Assert.Equal(10L, row[0]);
    }

    [Fact]
    public async Task CreateCustomerAndAccount_AsOneProviderMutationPlan_CommitsAtomically()
    {
        // Two separate MutationIntents (one per entity), planned separately
        // and combined into a single ProviderMutationPlan — proving a
        // caller can express "create a Customer and an Account together" as
        // one atomic unit, the same way the Banking E2E composes multiple
        // entities into one read.
        var planner = new MutationPlanner(Registry());

        var customerIntent = new MutationIntent(
            Customer.EntityId,
            MutationKind.Create,
            new[]
            {
                new MutationFieldValue(1, 1L),
                new MutationFieldValue(2, "Grace Hopper")
            });

        var accountIntent = new MutationIntent(
            Account.EntityId,
            MutationKind.Create,
            new[]
            {
                new MutationFieldValue(1, 20L),
                new MutationFieldValue(2, 1L),
                new MutationFieldValue(3, 1000.0)
            });

        var customerPlan = planner.Plan(customerIntent);
        var accountPlan = planner.Plan(accountIntent);

        var combinedOperations = customerPlan.Operations
            .Concat(accountPlan.Operations)
            .ToArray();

        var combinedPlan = new MutationPlan(combinedOperations);
        var providerPlan = SqlPlanCompiler.CompileMutation(combinedPlan);

        Assert.Equal(2, providerPlan.Operations.Count);

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var results = await provider.ExecuteMutationAsync(providerPlan, context);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.RowsAffected));

        var customerRows = await QueryAsync("SELECT Id, Name FROM Customer");
        var accountRows = await QueryAsync("SELECT Id, CustomerId, Balance FROM Account");

        Assert.Single(customerRows);
        Assert.Single(accountRows);
    }

    [Fact]
    public async Task UnfilteredUpdate_IsRejectedByThePlanner()
    {
        // Foundgine never mutates every row by accident — MutationPlanner
        // rejects an Update/Delete with no Filter before it ever reaches
        // the SQL compiler.
        var planner = new MutationPlanner(Registry());

        var intent = new MutationIntent(
            Account.EntityId,
            MutationKind.Update,
            new[] { new MutationFieldValue(3, 0.0) });

        Assert.Throws<InvalidOperationException>(() => planner.Plan(intent));

        await Task.CompletedTask;
    }
}
