using System.Globalization;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Execution.Mutation;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Mutation;
using Foundgine.Semantics.Query;
using Foundgine.Sql.Mutation;
using Foundgine.Sql.Mutation.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.Testing;

/// <summary>
/// P1 relational correctness suite. EF Core is deliberately used only as an
/// independent oracle; Foundgine never consumes the EF model at runtime.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FoundgineEfPostgresP1Tests(PostgresFixture fixture)
{
    [PostgreSqlFact]
    public async Task aggregate_count_filter_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var request = new SemanticRequest(
            CanonicalBanking.Customer,
            [new SemanticSelection(new FieldId(1), null, [])],
            new SemanticQueryOptions(
                Filter: new SemanticAggregateFilter(
                    CanonicalBanking.CustomerAccounts,
                    SemanticFilterAggregate.Count,
                    null,
                    SemanticAggregateFilterOperator.Gte,
                    2)));

        var actual = await FoundginePostgresHarness.ExecuteAsync(connection, request);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers
            .AsNoTracking()
            .Where(x => x.Accounts.Count >= 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var actualIds = actual.Rows
            .Select(x => Convert.ToInt32(x.Values["__fg_0_Id"], CultureInfo.InvariantCulture))
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(expected, actualIds);
    }

    [PostgreSqlFact]
    public async Task aggregate_max_filter_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var request = new SemanticRequest(
            CanonicalBanking.Customer,
            [new SemanticSelection(new FieldId(1), null, [])],
            new SemanticQueryOptions(
                Filter: new SemanticAggregateFilter(
                    CanonicalBanking.CustomerAccounts,
                    SemanticFilterAggregate.Max,
                    new FieldId(3),
                    SemanticAggregateFilterOperator.Gt,
                    100m)));

        var actual = await FoundginePostgresHarness.ExecuteAsync(connection, request);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers
            .AsNoTracking()
            .Where(x => x.Accounts.Max(a => (decimal?)a.Balance) > 100m)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var actualIds = actual.Rows
            .Select(x => Convert.ToInt32(x.Values["__fg_0_Id"], CultureInfo.InvariantCulture))
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(expected, actualIds);
    }

    [PostgreSqlFact]
    public async Task semantic_create_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var graph = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel())
            .Create("Customer")
            .Set("Id", 99)
            .Set("Name", "Differential")
            .Return("Id", "Name")
            .Build();

        var result = await ExecuteMutationAsync(connection, graph);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == 99);

        var mutationResult = Assert.Single(result.Results);
        Assert.Equal(99L, Convert.ToInt64(mutationResult.ReturnedValues![new FieldId(1)], CultureInfo.InvariantCulture));
        Assert.Equal(expected.Name, mutationResult.ReturnedValues[new FieldId(2)]?.ToString());
    }

    [PostgreSqlFact]
    public async Task semantic_update_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var graph = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel())
            .Update("Account")
            .Set("Balance", 150m)
            .Where("Id", SemanticFilterOperator.Eq, 10)
            .Return("Id", "Balance")
            .Build();

        var result = await ExecuteMutationAsync(connection, graph);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == 10);

        var mutationResult = Assert.Single(result.Results);
        Assert.Equal(1, mutationResult.AffectedRows);
        Assert.Equal(expected.Id, Convert.ToInt32(mutationResult.ReturnedValues![new FieldId(1)], CultureInfo.InvariantCulture));
        Assert.Equal(expected.Balance, Convert.ToDecimal(mutationResult.ReturnedValues[new FieldId(3)], CultureInfo.InvariantCulture));
    }

    [PostgreSqlFact]
    public async Task semantic_upsert_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var graph = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel())
            .Upsert("Account")
            .Set("Id", 10)
            .Set("CustomerId", 1)
            .Set("Balance", 222m)
            .Set("Status", "Active")
            .Conflict("Id")
            .Return("Id", "Balance")
            .Build();

        var result = await ExecuteMutationAsync(connection, graph);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == 10);

        var mutationResult = Assert.Single(result.Results);
        Assert.Equal(expected.Id, Convert.ToInt32(mutationResult.ReturnedValues![new FieldId(1)], CultureInfo.InvariantCulture));
        Assert.Equal(expected.Balance, Convert.ToDecimal(mutationResult.ReturnedValues[new FieldId(3)], CultureInfo.InvariantCulture));
    }

    [PostgreSqlFact]
    public async Task semantic_delete_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var graph = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel())
            .Delete("Transaction")
            .Where("Id", SemanticFilterOperator.Eq, 200)
            .Build();

        var result = await ExecuteMutationAsync(connection, graph);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var exists = await db.Transactions.AsNoTracking().AnyAsync(x => x.Id == 200);

        var mutationResult = Assert.Single(result.Results);
        Assert.Equal(1, mutationResult.AffectedRows);
        Assert.False(exists);
    }

    [PostgreSqlFact]
    public async Task mutation_batch_is_atomic_when_a_later_operation_violates_a_foreign_key()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var builder = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel());
        builder.Create("Transaction")
            .Set("Id", 901)
            .Set("AccountId", 10)
            .Set("Amount", 7m)
            .Set("TransactionDate", new DateTime(2026, 2, 1))
            .Return("Id");
        builder.Create("Transaction")
            .Set("Id", 902)
            .Set("AccountId", 999999)
            .Set("Amount", 8m)
            .Set("TransactionDate", new DateTime(2026, 2, 2))
            .Return("Id");

        var graph = builder.Build();
        var plan = new SemanticMutationPlanner().Plan(graph);
        var ir = new SemanticMutationExecutionLowerer(CanonicalBanking.BuildMetadata()).Lower(plan);
        var provider = new PostgresBatchedMutationExecutionProvider(connection, CanonicalBanking.BuildMetadata());

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            provider.ExecuteBatch(ir, new ExecutionContext());
            await Task.CompletedTask;
        });

        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM fg_query.\"Transaction\" WHERE \"Id\" IN (901, 902)", connection);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Assert.Equal(0, count);
    }

    [PostgreSqlFact]
    public async Task concurrent_same_key_upserts_preserve_a_single_row()
    {
        await using var setupConnection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(setupConnection);
        await setupConnection.CloseAsync();

        async Task Run(decimal balance)
        {
            await using var connection = await fixture.OpenConnectionAsync();
            var graph = new SemanticMutationIntentBuilder(CanonicalBanking.BuildModel())
                .Upsert("Account")
                .Set("Id", 77)
                .Set("CustomerId", 1)
                .Set("Balance", balance)
                .Set("Status", "Active")
                .Conflict("Id")
                .Return("Id")
                .Build();
            await ExecuteMutationAsync(connection, graph);
        }

        await Task.WhenAll(Run(10m), Run(20m));

        await using var verify = await fixture.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*), MIN(\"Balance\"), MAX(\"Balance\") FROM fg_query.\"Account\" WHERE \"Id\" = 77", verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Contains(reader.GetDecimal(1), new[] { 10m, 20m });
        Assert.Equal(reader.GetDecimal(1), reader.GetDecimal(2));
    }

    private static Task<MutationBatchResult> ExecuteMutationAsync(
        NpgsqlConnection connection,
        SemanticMutationOperationGraph graph)
    {
        var metadata = CanonicalBanking.BuildMetadata();
        var plan = new SemanticMutationPlanner().Plan(graph);
        var ir = new SemanticMutationExecutionLowerer(metadata).Lower(plan);
        var provider = new PostgresBatchedMutationExecutionProvider(connection, metadata);
        return Task.FromResult(provider.ExecuteBatch(ir, new ExecutionContext()));
    }
}
