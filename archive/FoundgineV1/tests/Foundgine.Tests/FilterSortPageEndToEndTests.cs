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
/// Milestone 6 (filtering) and Milestone 7 (sorting/paging) end-to-end:
/// same Customer -> Account -> Transaction domain as
/// <see cref="BankingEndToEndTests"/>, proving
///
///     QueryIntent.Filter / .Sort / .Page
///         -> QueryPlanner (FilterNode/SortNode/PageNode)
///         -> SqlPlanCompiler (SqlFilterNode/SqlSortNode/SqlPageNode)
///         -> SqlTextTranslator (WHERE/ORDER BY/LIMIT OFFSET + parameters)
///         -> SqlExecutionProvider (real SQLite, bound parameters)
///
/// end to end, with no step faked or skipped.
/// </summary>
public sealed class FilterSortPageEndToEndTests : IAsyncLifetime
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
            new(new ColumnId(2), "Name"),
        });

    private static readonly EntityMetadata Account = new(
        new EntityId(2),
        "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "Balance"),
        });

    private static readonly JoinMetadata AccountToCustomer = new(
        new JoinCondition(
            new ColumnReference(Account, 2),
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

            INSERT INTO Customer (Id, Name) VALUES (1, 'Bob');
            INSERT INTO Customer (Id, Name) VALUES (2, 'Ada Lovelace');
            INSERT INTO Customer (Id, Name) VALUES (3, 'Grace Hopper');
            INSERT INTO Customer (Id, Name) VALUES (4, 'Bob');

            INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 50.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (20, 2, 500.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (30, 3, 1000.0);
            INSERT INTO Account (Id, CustomerId, Balance) VALUES (40, 4, 150.0);
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

    private static JoinGraph Joins()
    {
        var joinGraph = new JoinGraph();
        joinGraph.AddEdge(Customer.EntityId, Account.EntityId, AccountToCustomer);
        return joinGraph;
    }

    private async Task<List<ExecutionRow>> ExecuteAsync(QueryIntent intent)
    {
        var planner = new QueryPlanner(Registry(), Joins());
        var queryPlan = planner.Plan(intent);
        var providerPlan = SqlPlanCompiler.Compile(queryPlan);
        var provider = new SqlExecutionProvider();

        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?>
            {
                ["ConnectionString"] = _connectionString,
            });

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        return rows;
    }

    private static string CustomerName(ExecutionRow row) =>
        (string)row.Single(Customer.EntityId)[1]!;

    [Fact]
    public async Task Customer_Name_Equals_Bob_FiltersToOnlyMatchingRows()
    {
        // Customer.Name = "Bob" — Milestone 6's own example.
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            filter: new ComparisonFilter(
                new ColumnReference(Customer, 2),
                ComparisonOperator.Equal,
                "Bob"));

        var rows = await ExecuteAsync(intent);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("Bob", CustomerName(row)));
    }

    [Fact]
    public async Task Account_Balance_GreaterThan_100_FiltersAcrossTheJoin()
    {
        // Account.Balance > 100 — the filter column lives on the joined
        // entity, not the root, proving alias resolution isn't hardcoded
        // to Customer.
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: new[] { Account.EntityId },
            filter: new ComparisonFilter(
                new ColumnReference(Account, 3),
                ComparisonOperator.GreaterThan,
                100.0));

        var rows = await ExecuteAsync(intent);

        // Accounts 20 (500), 30 (1000), 40 (150) qualify; account 10 (50) does not.
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task CombinedAndFilter_Customer_Name_And_Account_Balance()
    {
        // Customer.Name = "Bob" AND Account.Balance > 100
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: new[] { Account.EntityId },
            filter: new CompositeFilter(
                FilterCombinator.And,
                new FilterExpression[]
                {
                    new ComparisonFilter(
                        new ColumnReference(Customer, 2),
                        ComparisonOperator.Equal,
                        "Bob"),
                    new ComparisonFilter(
                        new ColumnReference(Account, 3),
                        ComparisonOperator.GreaterThan,
                        100.0),
                }));

        var rows = await ExecuteAsync(intent);

        // Of Bob's two accounts (50 and 150), only the 150 one qualifies.
        var row = Assert.Single(rows);
        Assert.Equal("Bob", CustomerName(row));
        Assert.Equal(150.0, row.Single(Account.EntityId)[2]);
    }

    [Fact]
    public async Task Sort_Customer_ByName_Ascending()
    {
        // ORDER BY Customer.Name — the doc's own sorting example.
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            sort: new[] { new SortTerm(new ColumnReference(Customer, 2)) });

        var rows = await ExecuteAsync(intent);

        var names = rows.Select(CustomerName).ToArray();
        Assert.Equal(new[] { "Ada Lovelace", "Bob", "Bob", "Grace Hopper" }, names);
    }

    [Fact]
    public async Task Sort_Customer_ByName_Descending()
    {
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            sort: new[]
            {
                new SortTerm(new ColumnReference(Customer, 2), SortDirection.Descending),
            });

        var rows = await ExecuteAsync(intent);

        var names = rows.Select(CustomerName).ToArray();
        Assert.Equal(new[] { "Grace Hopper", "Bob", "Bob", "Ada Lovelace" }, names);
    }

    [Fact]
    public async Task Page_LimitOnly_CapsTheResultSet()
    {
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            sort: new[] { new SortTerm(new ColumnReference(Customer, 2)) },
            page: new PageSpec(Limit: 2));

        var rows = await ExecuteAsync(intent);

        Assert.Equal(2, rows.Count);
        Assert.Equal(
            new[] { "Ada Lovelace", "Bob" },
            rows.Select(CustomerName).ToArray());
    }

    [Fact]
    public async Task Page_LimitAndOffset_ReturnsThePageWindow()
    {
        // ORDER BY Name LIMIT 2 OFFSET 1 — page 2 of 2-row pages, sorted.
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            sort: new[] { new SortTerm(new ColumnReference(Customer, 2)) },
            page: new PageSpec(Limit: 2, Offset: 1));

        var rows = await ExecuteAsync(intent);

        Assert.Equal(2, rows.Count);
        Assert.Equal(
            new[] { "Bob", "Bob" },
            rows.Select(CustomerName).ToArray());
    }

    [Fact]
    public async Task Page_OffsetOnly_SkipsRowsWithNoCap()
    {
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: Array.Empty<EntityId>(),
            sort: new[] { new SortTerm(new ColumnReference(Customer, 2)) },
            page: new PageSpec(Offset: 3));

        var rows = await ExecuteAsync(intent);

        var row = Assert.Single(rows);
        Assert.Equal("Grace Hopper", CustomerName(row));
    }

    [Fact]
    public async Task FilterSortAndPage_ComposeTogether()
    {
        // Account.Balance > 50, ordered by Customer.Name, first page of 2.
        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: new[] { Account.EntityId },
            filter: new ComparisonFilter(
                new ColumnReference(Account, 3),
                ComparisonOperator.GreaterThan,
                50.0),
            sort: new[] { new SortTerm(new ColumnReference(Customer, 2)) },
            page: new PageSpec(Limit: 2));

        var rows = await ExecuteAsync(intent);

        Assert.Equal(2, rows.Count);
        Assert.Equal(
            new[] { "Ada Lovelace", "Bob" },
            rows.Select(CustomerName).ToArray());
    }
}
