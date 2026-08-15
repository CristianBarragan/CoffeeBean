using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Metadata;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Mutation;
using Foundgine.Semantics.Query;
using Foundgine.Sql;
using Foundgine.Sql.Mutation;
using Foundgine.Sql.Mutation.Postgres;
using Npgsql;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>
/// PostgreSQL E2E integration proof for a stateful interaction sequence:
///
///   mutation -> query -> mutation -> query
///
/// The same PostgreSQL transaction is shared by every operation. The first
/// mutation creates a customer/account graph; the first query must observe the
/// newly-created state. A second semantic mutation then changes the account
/// status, and the second query must observe the changed state and exclude that
/// customer. The entire scenario is rolled back at the end.
///
/// This proves that the semantic, execution, SQL compilation and PostgreSQL
/// layers agree on state across multiple requests rather than only proving each
/// operation in isolation.
/// </summary>
public sealed class MutationQueryMutationQueryIntegrationE2ETests
{
    private const string ConnectionEnvironmentVariable = "FOUNDGINE_POSTGRES_CONNECTION_STRING";

    // SkippableFact (not [Fact]): when FOUNDGINE_POSTGRES_CONNECTION_STRING is
    // unset this test is reported as SKIPPED, not PASSED. A plain [Fact] with
    // an early `return;` shows as a pass, which lets CI go green without ever
    // touching PostgreSQL — silently defeating the point of a "flagship
    // stateful proof" test if the env var is ever misconfigured.
    [PostgreSqlFact]
    public async Task PostgresE2E_mutation_query_mutation_query_is_stateful_end_to_end_against_postgresql17()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var isolation = await BeginIsolatedSchemaAsync(connection);
        await PrepareUnifiedDatabaseAsync(connection);

        var mutationMetadata = PostgresE2ETests.BuildMetadata();
        var queryMetadata = ComplexQueryPostgresE2ETests.BuildMetadata();
        var metadata = MergeMetadata(mutationMetadata, queryMetadata);

        // Seed the read side using the same physical schema the mutation side uses.
        await SeedQueryBaselineAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            // -----------------------------------------------------------------
            // Doc claim 1 (POSTGRES_E2E_MUTATION_QUERY_MUTATION_QUERY.md):
            // "Mutation #1 creates a generated Customer/Account graph."
            // 1. MUTATION: create the complex PostgreSQL E2E graph.
            // -----------------------------------------------------------------
            var mutationGraph = ComplexSemanticMutationE2ETests.BuildGraph();
            var mutationPlan = new SemanticMutationPlanner().Plan(mutationGraph);
            var mutationIr = new SemanticMutationExecutionLowerer(metadata).Lower(mutationPlan);
            mutationIr.ValidateDerivedDependencies();

            var mutationSql = new PostgresBatchedMutationCompiler(metadata).Compile(mutationIr);
            Assert.NotEmpty(mutationSql.CommandText);
            Assert.Contains("unnest", mutationSql.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("__fg_corr", mutationSql.CommandText, StringComparison.Ordinal);

            var mutationProvider = new PostgresBatchedMutationExecutionProvider(
                connection, metadata, transaction);
            var firstMutation = mutationProvider.ExecuteBatch(mutationIr, new ExecutionContext());

            Assert.Equal(10, firstMutation.Results.Count);

            // Operation 0 in ComplexSemanticMutationE2ETests.BuildGraph()
            // is "Create Customer", returning Id/Name/Status. We reference the
            // builder's own EntityId/FieldId constants (rather than restating
            // literals like `new EntityId(702)` / `new FieldId(1)`) so that a
            // future reordering of BuildGraph() fails loudly at compile time or
            // via the builder's own operation-order assertions, instead of
            // silently pointing this test at the wrong operation/entity.
            var createdCustomerId = Convert.ToInt64(
                firstMutation.Results[0].ReturnedFields![ComplexSemanticMutationE2ETests.Id]);
            Assert.True(createdCustomerId > 0);

            // -----------------------------------------------------------------
            // Doc claim 2: "Query #1 observes that newly-created graph through
            // a nested relationship."
            // 2. QUERY: the query must observe the customer/account just created
            //    by mutation #1 in the SAME PostgreSQL transaction.
            // -----------------------------------------------------------------
            var queryModel = ComplexQueryPostgresE2ETests.BuildModel();
            var queryRequest = ComplexQueryPostgresE2ETests.BuildComplexRequest(50, 3);
            var queryPlan = ComplexQueryPostgresE2ETests.Compile(
                queryModel, metadata, queryRequest);

            Assert.Contains("EXISTS", queryPlan.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NOT EXISTS", queryPlan.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COUNT(*)", queryPlan.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", queryPlan.CommandText, StringComparison.OrdinalIgnoreCase);

            var queryProvider = new SqlExecutionProvider(connection, transaction);
            var firstQuery = await queryProvider.ExecuteAsync(queryPlan, new ExecutionContext());

            var firstQueryCustomerIds = firstQuery.Rows
                .Select(r => Convert.ToInt64(r.Values["__fg_0_Id"]))
                .Distinct()
                .ToArray();

            Assert.Contains(createdCustomerId, firstQueryCustomerIds);

            // The first query also proves that the Account inserted/upserted by
            // mutation #1 is physically visible through a nested relationship.
            var createdRows = firstQuery.Rows
                .Where(r => Convert.ToInt64(r.Values["__fg_0_Id"]) == createdCustomerId)
                .ToArray();
            Assert.NotEmpty(createdRows);
            Assert.All(createdRows, row => Assert.Equal("Primary", row.Values["__fg_1_Name"]));

            // -----------------------------------------------------------------
            // Doc claim 3: "Mutation #2 changes the generated Account from
            // Open to Blocked through Foundgine semantics, not raw SQL."
            // 3. MUTATION: change the account created by mutation #1 from Open
            //    to Blocked. This is a second, independent semantic mutation
            //    request, not a direct SQL update.
            // -----------------------------------------------------------------
            // Scoped to CustomerId == createdCustomerId in addition to
            // Name/Status: without the CustomerId condition this update would
            // silently match *any* Account named "Primary" with Status "Open"
            // (e.g. another customer created the same way), which would let
            // this test pass for the wrong reason. This exact risk is what
            // Second_mutation_only_blocks_the_intended_customers_account below
            // proves against a second, independently-created customer. Also
            // uses the builder's own constants instead of restating
            // EntityId(702)/FieldId(2)/FieldId(3).
            var secondMutationGraph = BuildBlockAccountGraph(createdCustomerId);

            var secondMutationPlan = new SemanticMutationPlanner().Plan(secondMutationGraph);
            var secondMutationIr = new SemanticMutationExecutionLowerer(metadata).Lower(secondMutationPlan);
            secondMutationIr.ValidateDerivedDependencies();

            var secondMutationSql = new PostgresBatchedMutationCompiler(metadata).Compile(secondMutationIr);
            Assert.NotEmpty(secondMutationSql.CommandText);

            var secondMutation = mutationProvider.ExecuteBatch(secondMutationIr, new ExecutionContext());
            Assert.Single(secondMutation.Results);

            // Exactly one row, not merely "at least one": the whole point of
            // this test is that the second mutation changes precisely the
            // graph created by the first mutation and nothing else. A looser
            // `>= 1` here would still pass if the filter above accidentally
            // matched extra rows.
            Assert.Equal(1, secondMutation.Results[0].AffectedRows);
            Assert.Equal(
                "Blocked",
                secondMutation.Results[0].ReturnedFields![ComplexSemanticMutationE2ETests.Status]?.ToString());

            // -----------------------------------------------------------------
            // Doc claim 4: "Query #2 observes the changed state and excludes
            // the generated Customer because its only Account is now blocked."
            // 4. QUERY: the exact same semantic query must now observe the changed
            //    state and exclude the customer because its only account is now
            //    Blocked. This is the key interaction assertion.
            // -----------------------------------------------------------------
            var secondQuery = await queryProvider.ExecuteAsync(queryPlan, new ExecutionContext());
            var secondQueryCustomerIds = secondQuery.Rows
                .Select(r => Convert.ToInt64(r.Values["__fg_0_Id"]))
                .Distinct()
                .ToArray();

            Assert.DoesNotContain(createdCustomerId, secondQueryCustomerIds);

            // Doc claim 5: "Existing baseline customers remain visible, proving
            // the second mutation changed the intended graph rather than
            // invalidating the whole query."
            Assert.Contains(1L, secondQueryCustomerIds);
            Assert.Contains(3L, secondQueryCustomerIds);

            // The result set shrank by exactly one customer (the one whose
            // account was just blocked) and did not otherwise change shape.
            // This rules out the query having been invalidated wholesale or
            // having coincidentally dropped/gained unrelated rows.
            Assert.Equal(firstQueryCustomerIds.Length - 1, secondQueryCustomerIds.Length);
            Assert.Subset(
                new HashSet<long>(firstQueryCustomerIds),
                new HashSet<long>(secondQueryCustomerIds));
        }
        finally
        {
            // Doc claim 6: "The outer transaction is rolled back, so the
            // database is left unchanged."
            await transaction.RollbackAsync();
        }
    }

    [PostgreSqlFact]
    public async Task PostgresE2E_explore_query_mutation_query_mutation_is_stateful_end_to_end_against_postgresql17()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var isolation = await BeginIsolatedSchemaAsync(connection);
        await PrepareUnifiedDatabaseAsync(connection);

        var mutationMetadata = PostgresE2ETests.BuildMetadata();
        var queryMetadata = ComplexQueryPostgresE2ETests.BuildMetadata();
        var metadata = MergeMetadata(mutationMetadata, queryMetadata);

        // PASS 1 — explore: resolve the semantic topology before touching data.
        Assert.NotEmpty(metadata.Entities);
        Assert.NotEmpty(metadata.Relationships);
        Assert.All(metadata.Entities, entity => Assert.NotEmpty(entity.Name));

        await SeedQueryBaselineAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var queryModel = ComplexQueryPostgresE2ETests.BuildModel();
            var queryPlan = ComplexQueryPostgresE2ETests.Compile(
                queryModel,
                metadata,
                ComplexQueryPostgresE2ETests.BuildComplexRequest(50, 3));

            var queryProvider = new SqlExecutionProvider(connection, transaction);
            var mutationProvider = new PostgresBatchedMutationExecutionProvider(
                connection, metadata, transaction);

            // PASS 2 — query: establish the pre-mutation state.
            var beforeMutation = await queryProvider.ExecuteAsync(
                queryPlan, new ExecutionContext());
            var beforeIds = beforeMutation.Rows
                .Select(r => Convert.ToInt64(r.Values["__fg_0_Id"]))
                .Distinct()
                .ToHashSet();

            Assert.Contains(1L, beforeIds);
            Assert.Contains(3L, beforeIds);

            // PASS 3 — mutation: create the graph through semantic mutation.
            var createGraph = ComplexSemanticMutationE2ETests.BuildGraph();
            var createPlan = new SemanticMutationPlanner().Plan(createGraph);
            var createIr = new SemanticMutationExecutionLowerer(metadata).Lower(createPlan);
            createIr.ValidateDerivedDependencies();

            var created = mutationProvider.ExecuteBatch(createIr, new ExecutionContext());
            var createdCustomerId = Convert.ToInt64(
                created.Results[0].ReturnedFields![ComplexSemanticMutationE2ETests.Id]);

            Assert.True(createdCustomerId > 0);

            // PASS 4 — query: the same query now sees the newly-created graph.
            var afterCreate = await queryProvider.ExecuteAsync(
                queryPlan, new ExecutionContext());
            var afterCreateIds = afterCreate.Rows
                .Select(r => Convert.ToInt64(r.Values["__fg_0_Id"]))
                .Distinct()
                .ToHashSet();

            Assert.Contains(createdCustomerId, afterCreateIds);
            Assert.True(afterCreateIds.IsSupersetOf(beforeIds));

            // PASS 5 — mutation: change only the newly-created account.
            var blockGraph = BuildBlockAccountGraph(createdCustomerId);
            var blockPlan = new SemanticMutationPlanner().Plan(blockGraph);
            var blockIr = new SemanticMutationExecutionLowerer(metadata).Lower(blockPlan);
            blockIr.ValidateDerivedDependencies();

            var blocked = mutationProvider.ExecuteBatch(blockIr, new ExecutionContext());

            Assert.Single(blocked.Results);
            Assert.Equal(1, blocked.Results[0].AffectedRows);
            Assert.Equal(
                "Blocked",
                blocked.Results[0].ReturnedFields![ComplexSemanticMutationE2ETests.Status]?.ToString());
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    /// <summary>
    /// Negative counterpart to the main scenario above. It exists to prove
    /// that scoping the second mutation's filter by CustomerId (in addition
    /// to Name/Status) is doing real work, not just defensive-looking
    /// decoration.
    ///
    /// The main test's second mutation filters on
    /// Name == "Primary" AND Status == "Open" AND CustomerId == created.
    /// If the CustomerId condition were ever dropped, the main test would
    /// still pass by coincidence, because it only ever creates one such
    /// account. Here we run ComplexSemanticMutationE2ETests'
    /// Create/Upsert graph *twice* against the same transaction, producing
    /// two independent customers who each get an Account named "Primary"
    /// with Status "Open" (the values BuildGraph() always uses). We then
    /// block only the first customer's account and assert the second is
    /// untouched — an unscoped filter would match both rows and either fail
    /// AffectedRows == 1 or block the wrong customer's account.
    ///
    /// This is built via two independent mutations rather than a raw SQL
    /// decoy row because PrepareUnifiedDatabaseAsync declares a genuinely
    /// unique index on Account.CustomerId (required as the ON CONFLICT
    /// arbiter for the Upsert in mutation #1) — a customer can only ever
    /// have one Account row in this schema, so two customers are needed to
    /// produce two "Primary"/"Open" rows.
    /// </summary>
    [PostgreSqlFact]
    public async Task Second_mutation_only_blocks_the_intended_customers_account()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var isolation = await BeginIsolatedSchemaAsync(connection);
        await PrepareUnifiedDatabaseAsync(connection);

        var mutationMetadata = PostgresE2ETests.BuildMetadata();
        var queryMetadata = ComplexQueryPostgresE2ETests.BuildMetadata();
        var metadata = MergeMetadata(mutationMetadata, queryMetadata);

        await SeedQueryBaselineAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var mutationProvider = new PostgresBatchedMutationExecutionProvider(
                connection, metadata, transaction);

            var firstCustomerId = CreatePrimaryOpenCustomer(mutationProvider, metadata);
            var secondCustomerId = CreatePrimaryOpenCustomer(mutationProvider, metadata);
            Assert.NotEqual(firstCustomerId, secondCustomerId);

            var blockGraph = BuildBlockAccountGraph(firstCustomerId);
            var blockPlan = new SemanticMutationPlanner().Plan(blockGraph);
            var blockIr = new SemanticMutationExecutionLowerer(metadata).Lower(blockPlan);
            blockIr.ValidateDerivedDependencies();

            var blockResult = mutationProvider.ExecuteBatch(blockIr, new ExecutionContext());

            // Exactly one row touched: the CustomerId condition is the only
            // thing distinguishing the two otherwise-identical
            // "Primary"/"Open" accounts. Without it, both would match.
            Assert.Single(blockResult.Results);
            Assert.Equal(1, blockResult.Results[0].AffectedRows);

            const string statusCheckSql = """
                SELECT "Status" FROM "Account" WHERE "CustomerId" = @customerId;
                """;

            await using var firstCheck = new NpgsqlCommand(statusCheckSql, connection, transaction);
            firstCheck.Parameters.AddWithValue("customerId", firstCustomerId);
            Assert.Equal("Blocked", (string?)await firstCheck.ExecuteScalarAsync());

            await using var secondCheck = new NpgsqlCommand(statusCheckSql, connection, transaction);
            secondCheck.Parameters.AddWithValue("customerId", secondCustomerId);
            Assert.Equal("Open", (string?)await secondCheck.ExecuteScalarAsync());
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static long CreatePrimaryOpenCustomer(
        PostgresBatchedMutationExecutionProvider mutationProvider,
        MetadataRegistry metadata)
    {
        var graph = ComplexSemanticMutationE2ETests.BuildGraph();
        var plan = new SemanticMutationPlanner().Plan(graph);
        var ir = new SemanticMutationExecutionLowerer(metadata).Lower(plan);
        ir.ValidateDerivedDependencies();

        var result = mutationProvider.ExecuteBatch(ir, new ExecutionContext());
        return Convert.ToInt64(
            result.Results[0].ReturnedFields![ComplexSemanticMutationE2ETests.Id]);
    }

    private static SemanticMutationOperationGraph BuildBlockAccountGraph(long customerId)
    {
        return new SemanticMutationOperationGraph(
        [
            new SemanticMutationOperation(
                ComplexSemanticMutationE2ETests.Account,
                SemanticMutationKind.Update,
                [new SemanticMutationField(ComplexSemanticMutationE2ETests.Status, "Blocked")],
                new SemanticAndFilter(
                [
                    new SemanticFieldFilter(
                        ComplexSemanticMutationE2ETests.CustomerId,
                        SemanticFilterOperator.Eq,
                        customerId),
                    new SemanticFieldFilter(
                        ComplexSemanticMutationE2ETests.Name,
                        SemanticFilterOperator.Eq,
                        "Primary"),
                    new SemanticFieldFilter(
                        ComplexSemanticMutationE2ETests.Status,
                        SemanticFilterOperator.Eq,
                        "Open")
                ]),
                ConflictFields: [],
                ReturnFields:
                [
                    ComplexSemanticMutationE2ETests.Id,
                    ComplexSemanticMutationE2ETests.Status
                ],
                Effects:
                [
                    new(SemanticMutationEffectKind.UpdateEntity, ComplexSemanticMutationE2ETests.Account),
                    new(SemanticMutationEffectKind.SetField, ComplexSemanticMutationE2ETests.Account, ComplexSemanticMutationE2ETests.Status)
                ],
                Dependencies: [])
        ]);
    }

    // NOTE: this assumes the mutation-side and query-side metadata registries
    // never share an EntityId/RelationshipId. That happens to hold today
    // because ComplexSemanticMutationE2ETests uses IDs in the
    // 700s/1-6 range and ComplexQueryPostgresE2ETests uses a
    // disjoint range, but nothing here enforces it. If either side's ID
    // ranges are ever changed, MetadataRegistry.Register will need to surface
    // a collision loudly (or this helper should assert on it) rather than
    // silently letting one registration overwrite the other.
    private static MetadataRegistry MergeMetadata(MetadataRegistry left, MetadataRegistry right)
    {
        var merged = new MetadataRegistry();
        foreach (var entity in left.Entities) merged.Register(entity);
        foreach (var relationship in left.Relationships) merged.Register(relationship);
        foreach (var entity in right.Entities) merged.Register(entity);
        foreach (var relationship in right.Relationships) merged.Register(relationship);
        return merged;
    }

    private static async Task<IsolatedSchema> BeginIsolatedSchemaAsync(NpgsqlConnection connection)
    {
        var schema = "fg_e2e_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; SET search_path TO \"{schema}\";", connection))
            await create.ExecuteNonQueryAsync();
        return new IsolatedSchema(connection, schema);
    }

    private sealed class IsolatedSchema : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly string _schema;
        public IsolatedSchema(NpgsqlConnection connection, string schema)
        {
            _connection = connection;
            _schema = schema;
        }
        public async ValueTask DisposeAsync()
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE; SET search_path TO public;", _connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task PrepareUnifiedDatabaseAsync(NpgsqlConnection connection)
    {
        const string sql = """
            DROP TABLE IF EXISTS "Transaction" CASCADE;
            DROP TABLE IF EXISTS "Payment" CASCADE;
            DROP TABLE IF EXISTS "Order" CASCADE;
            DROP TABLE IF EXISTS "Audit" CASCADE;
            DROP TABLE IF EXISTS "Profile" CASCADE;
            DROP TABLE IF EXISTS "Account" CASCADE;
            DROP TABLE IF EXISTS "Customer" CASCADE;

            CREATE TABLE "Customer" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "Name" text NOT NULL,
                "Status" text NOT NULL,
                "Notes" text
            );
            CREATE TABLE "Profile" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CustomerId" bigint NOT NULL REFERENCES "Customer"("Id"),
                "Name" text NOT NULL
            );
            CREATE TABLE "Account" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CustomerId" bigint NOT NULL REFERENCES "Customer"("Id"),
                "Name" text NOT NULL DEFAULT 'Seed',
                "Status" text NOT NULL,
                "Balance" numeric NOT NULL DEFAULT 150
            );
            CREATE TABLE "Order" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "AccountId" bigint NOT NULL REFERENCES "Account"("Id"),
                "Status" text NOT NULL
            );
            CREATE TABLE "Payment" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "OrderId" bigint NOT NULL REFERENCES "Order"("Id"),
                "Amount" numeric NOT NULL,
                "Status" text NOT NULL
            );
            CREATE TABLE "Audit" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CustomerId" bigint NOT NULL REFERENCES "Customer"("Id"),
                "Kind" text NOT NULL
            );
            CREATE TABLE "Transaction" (
                "Id" bigint PRIMARY KEY,
                "AccountId" bigint NOT NULL REFERENCES "Account"("Id"),
                "Amount" numeric NOT NULL,
                "TransactionDate" timestamp NOT NULL
            );

            CREATE UNIQUE INDEX "UX_Account_CustomerId" ON "Account"("CustomerId");
            CREATE INDEX "IX_Profile_CustomerId" ON "Profile"("CustomerId");
            CREATE INDEX "IX_Order_AccountId" ON "Order"("AccountId");
            CREATE INDEX "IX_Audit_CustomerId" ON "Audit"("CustomerId");
            CREATE INDEX "IX_Transaction_AccountId" ON "Transaction"("AccountId");
            CREATE INDEX "IX_Transaction_Amount" ON "Transaction"("Amount");
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedQueryBaselineAsync(NpgsqlConnection connection)
    {
        // NOTE: exactly one Account per baseline Customer. PrepareUnifiedDatabaseAsync
        // declares "UX_Account_CustomerId" as a *fully* unique index (required as the
        // ON CONFLICT("CustomerId") arbiter for mutation #1's Account Upsert — Postgres
        // requires a real unique index/constraint to accept that clause). The original
        // version of this seed inserted two Account rows per customer, which is legal
        // under the *non-unique* per-customer index used by the standalone
        // ComplexQueryPostgresE2ETests, but violates the unique index this
        // merged schema actually declares. That mismatch meant this seed step would
        // throw a unique-constraint violation the moment it ran against real
        // PostgreSQL — a bug that the silent `if (no connection string) return;` skip
        // pattern (see the [PostgreSqlFact] fix above) could hide indefinitely, since
        // the test always no-ops without a live database.
        //
        // Customer 2 still ends up excluded from the complex query below: instead of
        // failing the "no Closed account" (None) predicate via a second account, its
        // single Blocked account now fails the "no Blocked account" (All) predicate
        // instead. The net query result is unchanged for the customers this test
        // actually asserts on (1 and 3 remain visible; 2 remains excluded).
        const string sql = """
            INSERT INTO "Customer"("Id","Name","Status") VALUES
                (1,'Alice','Active'),
                (2,'Bob','Active'),
                (3,'Carol','Active');

            INSERT INTO "Account"("Id","CustomerId","Name","Balance","Status") VALUES
                (10,1,'Seed',150,'Active'),
                (20,2,'Seed',25,'Blocked'),
                (30,3,'Seed',150,'Active');

            INSERT INTO "Transaction"("Id","AccountId","Amount","TransactionDate") VALUES
                (100,10,300,'2026-01-01'),
                (200,20,30,'2026-01-02'),
                (300,30,300,'2026-01-01');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
