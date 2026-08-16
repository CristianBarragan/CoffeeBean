using System.Text.Json;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Query;
using Foundgine.Semantics.Resolution;
using Foundgine.Sql;
using Npgsql;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>
/// Flagship read proof. A single intentionally complex semantic request is
/// resolved, authorized, planned, lowered into provider-independent Execution IR,
/// compiled to SQL, executed through Npgsql against PostgreSQL 17, and validated
/// from actual database rows.
///
/// The query exercises nested selections, two-hop traversal, AND/OR composition,
/// Some/None/All relationship quantifiers, Count aggregation, IN/NEQ/GTE filters,
/// ordering and a parameterized limit. EXPLAIN ANALYZE is run separately in a
/// rollback-only transaction so the first read measurement gate can be collected
/// without changing the compiler.
/// </summary>
public sealed class ComplexQueryPostgresE2ETests
{
    private const string ConnectionEnvironmentVariable = "FOUNDGINE_POSTGRES_CONNECTION_STRING";

    private static readonly EntityId Customer = new(800);
    private static readonly EntityId Account = new(801);
    private static readonly EntityId Transaction = new(802);
    private static readonly RelationshipId CustomerAccounts = new(800);
    private static readonly RelationshipId AccountTransactions = new(801);

    [PostgreSqlFact]
    public async Task PostgresE2E_complex_query_runs_semantics_to_postgresql17()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var isolation = await BeginIsolatedSchemaAsync(connection);
        await PrepareDatabaseAsync(connection);

        var model = BuildModel();
        var metadata = BuildMetadata();
        await SeedAsync(connection);

        var request = BuildComplexRequest(limit: 50);
        var plan = Compile(model, metadata, request);

        Assert.Contains("EXISTS", plan.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", plan.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", plan.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" IN (", plan.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" <> ", plan.CommandText, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN", plan.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", plan.CommandText, StringComparison.OrdinalIgnoreCase);

        var stats = await ExplainAnalyzeAsync(connection, plan, limit: 50);
        PrintStats("complex", 50, 3, stats);
        Assert.True(stats.PlanningTimeMs >= 0);
        Assert.True(stats.ExecutionTimeMs >= 0);
        Assert.NotEmpty(stats.JoinStrategies);

        await using var transaction = await connection.BeginTransactionAsync();
        var provider = new SqlExecutionProvider(connection, transaction);
        var result = await provider.ExecuteAsync(plan, PaginationExecutionContext.Create(50));

        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, row =>
        {
            Assert.NotNull(row.Values["__fg_0_Id"]);
            Assert.NotNull(row.Values["__fg_0_Name"]);
            Assert.NotNull(row.Values["__fg_1_Id"]);
            Assert.NotNull(row.Values["__fg_1_Balance"]);
            Assert.NotNull(row.Values["__fg_2_Id"]);
            Assert.NotNull(row.Values["__fg_2_Amount"]);
        });

        var customerIds = result.Rows.Select(r => Convert.ToInt64(r.Values["__fg_0_Id"])).Distinct().ToArray();
        Assert.Contains(1L, customerIds);
        Assert.DoesNotContain(2L, customerIds); // blocked/closed account path fails semantic predicates.
        Assert.Contains(3L, customerIds);

        await transaction.RollbackAsync();
    }

    [PostgreSqlTheory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(50, 2)]
    [InlineData(500, 3)]
    public async Task PostgresE2E_complex_query_measurement_matrix(
        int datasetSize,
        int depth)
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var isolation = await BeginIsolatedSchemaAsync(connection);
        await PrepareDatabaseAsync(connection);
        await SeedScaledAsync(connection, datasetSize);

        var model = BuildModel();
        var metadata = BuildMetadata();
        var request = BuildComplexRequest(limit: Math.Max(datasetSize, 50), depth);
        var plan = Compile(model, metadata, request);
        var stats = await ExplainAnalyzeAsync(connection, plan, Math.Max(datasetSize, 50));

        PrintStats("matrix", datasetSize, depth, stats);
        PrintNodeEvidence(stats);

        Assert.True(stats.PlanningTimeMs >= 0);
        Assert.True(stats.ExecutionTimeMs >= 0);
        Assert.True(stats.RootActualRows >= 0);
        Assert.True(stats.RootPlanRows >= 0);
    }

    internal static SemanticRequest BuildComplexRequest(int limit, int depth = 3)
    {
        var selections = new List<SemanticSelection>
        {
            new(new FieldId(1), null, []),
            new(new FieldId(2), null, [])
        };

        if (depth >= 2)
        {
            var transactionSelections = new List<SemanticSelection>
            {
                new(new FieldId(1), null, []),
                new(new FieldId(3), null, []),
                new(new FieldId(4), null, [])
            };

            selections.Add(new SemanticSelection(
                null,
                CustomerAccounts,
                [
                    new SemanticSelection(new FieldId(1), null, []),
                    new SemanticSelection(new FieldId(3), null, []),
                    new SemanticSelection(null, AccountTransactions, transactionSelections)
                ]));
        }

        var accountSome = new SemanticRelationshipFilter(
            CustomerAccounts,
            SemanticRelationshipQuantifier.Some,
            new SemanticOrFilter(
                [
                    new SemanticFieldFilter(new FieldId(3), SemanticFilterOperator.In, new[] { 100m, 250m }),
                    new SemanticRelationshipFilter(
                        AccountTransactions,
                        SemanticRelationshipQuantifier.Some,
                        new SemanticFieldFilter(new FieldId(3), SemanticFilterOperator.In, new[] { 250m }))
                ]));

        var accountNone = new SemanticRelationshipFilter(
            CustomerAccounts,
            SemanticRelationshipQuantifier.None,
            new SemanticFieldFilter(new FieldId(4), SemanticFilterOperator.Eq, "Closed"));

        var accountAll = new SemanticRelationshipFilter(
            CustomerAccounts,
            SemanticRelationshipQuantifier.All,
            new SemanticFieldFilter(new FieldId(4), SemanticFilterOperator.Neq, "Blocked"));

        var filter = new SemanticAndFilter(
            [
            new SemanticFieldFilter(new FieldId(2), SemanticFilterOperator.In, new[] { "Alice", "Bob", "Carol", "Customer-10" }),
            accountSome,
            accountNone,
            accountAll,
            new SemanticAggregateFilter(
                CustomerAccounts,
                SemanticFilterAggregate.Count,
                null,
                SemanticAggregateFilterOperator.Gte,
                1)
            ]);

        return new SemanticRequest(
            Customer,
            selections,
            new SemanticQueryOptions(
                Filter: filter,
                Order: [new SemanticOrderTerm(new FieldId(2), SemanticSortDirection.Asc)],
                Limit: limit));
    }

    internal static SqlPlan Compile(SemanticModel model, IMetadataProvider metadata, SemanticRequest request)
    {
        var resolved = new SemanticRequestResolver(model).Resolve(request);
        var authorized = new SemanticAuthorizer(new AllowAllSemanticAuthorizationPolicy()).Authorize(resolved);
        var execution = new Planner().Plan(authorized);
        return new SqlCompiler(metadata).Compile(execution);
    }

    internal static SemanticModel BuildModel() =>
        new SemanticModelBuilder()
            .Entity(Customer, "Customer", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(2), "Name", typeof(string))
                .Relationship(CustomerAccounts, "Accounts", Account, RelationshipCardinality.Many))
            .Entity(Account, "Account", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Balance", typeof(decimal))
                .Field(new FieldId(4), "Status", typeof(string))
                .Relationship(AccountTransactions, "Transactions", Transaction, RelationshipCardinality.Many))
            .Entity(Transaction, "Transaction", e => e
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal))
                .Field(new FieldId(4), "TransactionDate", typeof(DateTime)))
            .Build();

    internal static MetadataRegistry BuildMetadata()
    {
        var registry = new MetadataRegistry();
        registry.Register(new EntityMetadata(Customer, "Customer",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "Name")],
            Fields: [
                new FieldMetadata(new FieldId(1), "Id", typeof(long), new ColumnReference(Customer, new ColumnId(1))),
                new FieldMetadata(new FieldId(2), "Name", typeof(string), new ColumnReference(Customer, new ColumnId(2)))],
            PrimaryKey: new ColumnReference(Customer, new ColumnId(1))));

        registry.Register(new EntityMetadata(Account, "Account",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "CustomerId"), new ColumnMetadata(new ColumnId(3), "Balance"), new ColumnMetadata(new ColumnId(4), "Status")],
            Fields: [
                new FieldMetadata(new FieldId(1), "Id", typeof(long), new ColumnReference(Account, new ColumnId(1))),
                new FieldMetadata(new FieldId(3), "Balance", typeof(decimal), new ColumnReference(Account, new ColumnId(3))),
                new FieldMetadata(new FieldId(4), "Status", typeof(string), new ColumnReference(Account, new ColumnId(4)))],
            PrimaryKey: new ColumnReference(Account, new ColumnId(1))));

        registry.Register(new EntityMetadata(Transaction, "Transaction",
            [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "AccountId"), new ColumnMetadata(new ColumnId(3), "Amount"), new ColumnMetadata(new ColumnId(4), "TransactionDate")],
            Fields: [
                new FieldMetadata(new FieldId(1), "Id", typeof(long), new ColumnReference(Transaction, new ColumnId(1))),
                new FieldMetadata(new FieldId(3), "Amount", typeof(decimal), new ColumnReference(Transaction, new ColumnId(3))),
                new FieldMetadata(new FieldId(4), "TransactionDate", typeof(DateTime), new ColumnReference(Transaction, new ColumnId(4)))],
            PrimaryKey: new ColumnReference(Transaction, new ColumnId(1))));

        registry.Register(new RelationshipMetadata(CustomerAccounts, Customer, Account, "Accounts",
            new ColumnReference(Customer, new ColumnId(1)), new ColumnReference(Account, new ColumnId(2))));
        registry.Register(new RelationshipMetadata(AccountTransactions, Account, Transaction, "Transactions",
            new ColumnReference(Account, new ColumnId(1)), new ColumnReference(Transaction, new ColumnId(2))));
        return registry;
    }

    private static async Task<IsolatedSchema> BeginIsolatedSchemaAsync(NpgsqlConnection connection)
    {
        var schema = "fg_read_" + Guid.NewGuid().ToString("N");
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; SET search_path TO \"{schema}\";", connection);
        await command.ExecuteNonQueryAsync();
        return new IsolatedSchema(connection, schema);
    }

    private sealed class IsolatedSchema : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly string _schema;
        public IsolatedSchema(NpgsqlConnection connection, string schema) { _connection = connection; _schema = schema; }
        public async ValueTask DisposeAsync()
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE; SET search_path TO public;", _connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    internal static async Task PrepareDatabaseAsync(NpgsqlConnection connection)
    {
        const string sql = """
            DROP TABLE IF EXISTS "Transaction" CASCADE;
            DROP TABLE IF EXISTS "Account" CASCADE;
            DROP TABLE IF EXISTS "Customer" CASCADE;
            CREATE TABLE "Customer" ("Id" bigint PRIMARY KEY, "Name" text NOT NULL);
            CREATE TABLE "Account" ("Id" bigint PRIMARY KEY, "CustomerId" bigint NOT NULL REFERENCES "Customer"("Id"), "Balance" numeric NOT NULL, "Status" text NOT NULL);
            CREATE TABLE "Transaction" ("Id" bigint PRIMARY KEY, "AccountId" bigint NOT NULL REFERENCES "Account"("Id"), "Amount" numeric NOT NULL, "TransactionDate" timestamp NOT NULL);
            CREATE INDEX "IX_Account_CustomerId" ON "Account"("CustomerId");
            CREATE INDEX "IX_Account_Status" ON "Account"("Status");
            CREATE INDEX "IX_Transaction_AccountId" ON "Transaction"("AccountId");
            CREATE INDEX "IX_Transaction_Amount" ON "Transaction"("Amount");
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task SeedAsync(NpgsqlConnection connection) => await SeedScaledAsync(connection, 10);

    internal static async Task SeedScaledAsync(NpgsqlConnection connection, int customers)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("", connection, transaction);

        // Npgsql prepares commands by default. PostgreSQL does not allow a
        // prepared statement to contain multiple SQL commands separated by
        // semicolons, so keep each seed operation as its own command while
        // retaining the single transaction for atomic fixture setup.
        for (var i = 1; i <= customers; i++)
        {
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO \"Customer\"(\"Id\",\"Name\") VALUES ($1,$2);";
            command.Parameters.AddWithValue(i);
            command.Parameters.AddWithValue(i switch { 1 => "Alice", 2 => "Bob", 3 => "Carol", _ => $"Customer-{i}" });
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO \"Account\"(\"Id\",\"CustomerId\",\"Balance\",\"Status\") VALUES ($1,$2,$3,$4);";
            command.Parameters.AddWithValue(i * 10L);
            command.Parameters.AddWithValue(i);
            command.Parameters.AddWithValue(i == 2 ? 50m : 250m);
            command.Parameters.AddWithValue(i == 2 ? "Closed" : "Active");
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO \"Account\"(\"Id\",\"CustomerId\",\"Balance\",\"Status\") VALUES ($1,$2,$3,$4);";
            command.Parameters.AddWithValue(i * 10L + 1);
            command.Parameters.AddWithValue(i);
            command.Parameters.AddWithValue(i == 2 ? 25m : 80m);
            command.Parameters.AddWithValue(i == 2 ? "Blocked" : "Active");
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO \"Transaction\"(\"Id\",\"AccountId\",\"Amount\",\"TransactionDate\") VALUES ($1,$2,$3,'2026-01-01');";
            command.Parameters.AddWithValue(i * 100L);
            command.Parameters.AddWithValue(i * 10L);
            command.Parameters.AddWithValue(i == 2 ? 20m : 300m);
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO \"Transaction\"(\"Id\",\"AccountId\",\"Amount\",\"TransactionDate\") VALUES ($1,$2,$3,'2026-01-02');";
            command.Parameters.AddWithValue(i * 100L + 1);
            command.Parameters.AddWithValue(i * 10L + 1);
            command.Parameters.AddWithValue(i == 2 ? 30m : 125m);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<PlanStats> ExplainAnalyzeAsync(NpgsqlConnection connection, SqlPlan plan, int limit)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new NpgsqlCommand(
                "EXPLAIN (ANALYZE, BUFFERS, WAL, FORMAT JSON) " + plan.CommandText,
                connection, transaction);
            foreach (var binding in plan.EffectiveParameters)
            {
                object value = binding.ContextPath is not null && binding.ContextPath == ExecutionContextKeys.PaginationLimit
                    ? limit
                    : binding.Value ?? DBNull.Value;
                command.Parameters.AddWithValue(binding.Name, value);
            }

            var raw = Convert.ToString(await command.ExecuteScalarAsync())
                ?? throw new InvalidOperationException("PostgreSQL returned no EXPLAIN JSON.");
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement[0];
            var stats = new PlanStats
            {
                PlanningTimeMs = root.GetProperty("Planning Time").GetDouble(),
                ExecutionTimeMs = root.GetProperty("Execution Time").GetDouble()
            };
            Walk(root.GetProperty("Plan"), stats);
            if (root.TryGetProperty("WAL", out var wal) && wal.TryGetProperty("WAL Bytes", out var bytes))
                stats.WalBytes = bytes.GetInt64();
            return stats;
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static void Walk(JsonElement node, PlanStats stats)
    {
        var type = node.TryGetProperty("Node Type", out var typeElement) ? typeElement.GetString() ?? "" : "";
        var estimated = node.TryGetProperty("Plan Rows", out var est) ? est.GetDouble() : 0;
        var actual = node.TryGetProperty("Actual Rows", out var act) ? act.GetDouble() : 0;
        var loops = node.TryGetProperty("Actual Loops", out var lp) ? lp.GetDouble() : 0;
        var time = node.TryGetProperty("Actual Total Time", out var tm) ? tm.GetDouble() : 0;
        stats.RootActualRows = Math.Max(stats.RootActualRows, (long)actual);
        stats.RootPlanRows = Math.Max(stats.RootPlanRows, (long)estimated);
        if (node.TryGetProperty("Shared Hit Blocks", out var hit)) stats.SharedHit += hit.GetInt64();
        if (node.TryGetProperty("Shared Read Blocks", out var read)) stats.SharedRead += read.GetInt64();
        if (node.TryGetProperty("Shared Written Blocks", out var written)) stats.SharedWritten += written.GetInt64();
        if (node.TryGetProperty("Temp Read Blocks", out var tempRead)) stats.TempRead += tempRead.GetInt64();
        if (node.TryGetProperty("Temp Written Blocks", out var tempWrite)) stats.TempWritten += tempWrite.GetInt64();
        stats.Nodes.Add(new PlanNodeStats(type, estimated, actual, loops, time,
            node.TryGetProperty("Sort Method", out var sm) ? sm.GetString() : null,
            node.TryGetProperty("Sort Space Used", out var su) ? su.GetInt64() : null,
            node.TryGetProperty("Sort Space Type", out var st) ? st.GetString() : null));
        if (type.Contains("Join", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Loop", StringComparison.OrdinalIgnoreCase))
            stats.JoinStrategies.Add(type);
        if (type == "Sort") stats.SortCount++;
        if (type.Contains("Materialize", StringComparison.OrdinalIgnoreCase)) stats.MaterializeCount++;
        if (node.TryGetProperty("Plans", out var children)) foreach (var child in children.EnumerateArray()) Walk(child, stats);
    }

    private sealed record PlanNodeStats(string NodeType, double EstimatedRows, double ActualRows, double ActualLoops, double ActualTotalTimeMs, string? SortMethod, long? SortSpaceUsed, string? SortSpaceType);

    private static void PrintStats(string kind, int datasetSize, int depth, PlanStats s) =>
        Console.WriteLine($"POSTGRES_E2E READ kind={kind} dataset={datasetSize} depth={depth} planning_ms={s.PlanningTimeMs:F3} execution_ms={s.ExecutionTimeMs:F3} shared_hit={s.SharedHit} shared_read={s.SharedRead} shared_written={s.SharedWritten} temp_read={s.TempRead} temp_written={s.TempWritten} wal_bytes={s.WalBytes} joins={string.Join(',', s.JoinStrategies)} sorts={s.SortCount} materialize={s.MaterializeCount} actual_rows={s.RootActualRows} estimated_rows={s.RootPlanRows}");

    private static void PrintNodeEvidence(PlanStats s)
    {
        foreach (var node in s.Nodes.Where(n => n.EstimatedRows > 0).OrderByDescending(n => Math.Max(n.ActualRows, 1) / Math.Max(n.EstimatedRows, 1)).Take(5))
            Console.WriteLine($"POSTGRES_E2E READ_NODE type={node.NodeType} estimated_rows={node.EstimatedRows:F0} actual_rows={node.ActualRows:F0} loops={node.ActualLoops:F0} time_ms={node.ActualTotalTimeMs:F3} sort_method={node.SortMethod ?? "-"} sort_space={node.SortSpaceUsed?.ToString() ?? "-"} sort_space_type={node.SortSpaceType ?? "-"}");
    }

    private sealed class PlanStats
    {
        public double PlanningTimeMs;
        public double ExecutionTimeMs;
        public long SharedHit;
        public long SharedRead;
        public long SharedWritten;
        public long TempRead;
        public long TempWritten;
        public long WalBytes;
        public long RootActualRows;
        public long RootPlanRows;
        public int SortCount;
        public int MaterializeCount;
        public HashSet<string> JoinStrategies { get; } = new(StringComparer.Ordinal);
        public List<PlanNodeStats> Nodes { get; } = new();
    }
}
