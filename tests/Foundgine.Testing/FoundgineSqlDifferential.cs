using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Resolution;
using Foundgine.Sql;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.Testing;

/// <summary>Small, reusable adapter for executing the canonical semantic read pipeline.</summary>
public static class FoundginePostgresHarness
{
    public static async Task<ExecutionResult> ExecuteAsync(
        NpgsqlConnection connection,
        SemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = CanonicalBanking.BuildModel();
        var metadata = CanonicalBanking.BuildMetadata();
        var resolved = new SemanticRequestResolver(model.Freeze().CreateSnapshot()).Resolve(request);
        var authorized = new SemanticAuthorizer(new AllowAllSemanticAuthorizationPolicy()).Authorize(resolved);
        var plan = new Planner().Plan(authorized) with
        {
            AuthorizationBinding = new SemanticPlanAuthorizationBinding("canonical-test", "canonical-test")
        };
        var sqlPlan = new SqlCompiler(metadata).Compile(plan);
        return await new SqlExecutionProvider(connection).ExecuteAsync(
            sqlPlan, new ExecutionContext(), cancellationToken);
    }
}

public static class DifferentialAssertions
{
    public static void EqualCustomerProjection(
        IReadOnlyList<CustomerRow> expected,
        ExecutionResult actual)
    {
        var actualRows = actual.Rows
            .Select(row => new
            {
                Id = Convert.ToInt32(row.Values["__fg_0_Id"], System.Globalization.CultureInfo.InvariantCulture),
                Name = Convert.ToString(row.Values["__fg_0_Name"], System.Globalization.CultureInfo.InvariantCulture)!
            })
            .OrderBy(x => x.Id)
            .ToArray();

        var expectedRows = expected
            .Select(x => new { x.Id, x.Name })
            .OrderBy(x => x.Id)
            .ToArray();

        Assert.Equal(expectedRows, actualRows);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class FoundgineEfPostgresDifferentialTests(PostgresFixture fixture)
{
    [PostgreSqlFact]
    public async Task projection_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var request = new SemanticRequest(
            CanonicalBanking.Customer,
            [
                new SemanticSelection(new FieldId(1), null, []),
                new SemanticSelection(new FieldId(2), null, [])
            ]);

        var actual = await FoundginePostgresHarness.ExecuteAsync(connection, request);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new CustomerRow { Id = x.Id, Name = x.Name })
            .ToListAsync();

        DifferentialAssertions.EqualCustomerProjection(expected, actual);
    }

    [PostgreSqlFact]
    public async Task predicate_matches_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var request = new SemanticRequest(
            CanonicalBanking.Customer,
            [
                new SemanticSelection(new FieldId(1), null, []),
                new SemanticSelection(new FieldId(2), null, [])
            ],
            new Foundgine.Semantics.Query.SemanticQueryOptions(
                new Foundgine.Semantics.Query.SemanticFieldFilter(
                    new FieldId(2),
                    Foundgine.Semantics.Query.SemanticFilterOperator.Eq,
                    "Alice")));

        var actual = await FoundginePostgresHarness.ExecuteAsync(connection, request);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers
            .AsNoTracking()
            .Where(x => x.Name == "Alice")
            .Select(x => new CustomerRow { Id = x.Id, Name = x.Name })
            .ToListAsync();

        DifferentialAssertions.EqualCustomerProjection(expected, actual);
    }

    [PostgreSqlFact]
    public async Task relationship_rows_match_ef_reference()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);

        var request = new SemanticRequest(
            CanonicalBanking.Customer,
            [
                new SemanticSelection(new FieldId(1), null, []),
                new SemanticSelection(new FieldId(2), null, []),
                new SemanticSelection(null, CanonicalBanking.CustomerAccounts,
                [
                    new SemanticSelection(new FieldId(1), null, []),
                    new SemanticSelection(new FieldId(3), null, [])
                ])
            ]);

        var actual = await FoundginePostgresHarness.ExecuteAsync(connection, request);

        var options = new DbContextOptionsBuilder<CanonicalBankingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        await using var db = new CanonicalBankingDbContext(options);
        var expected = await db.Customers
            .AsNoTracking()
            .Include(x => x.Accounts)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(expected.Count, actual.Rows.Select(x => x.Values["__fg_0_Id"]).Distinct().Count());
        var expectedAccounts = expected.SelectMany(x => x.Accounts).OrderBy(x => x.Id).Select(x => x.Id).ToArray();
        var actualAccounts = actual.Rows
            .Select(x => Convert.ToInt32(x.Values["__fg_1_Id"], System.Globalization.CultureInfo.InvariantCulture))
            .Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(expectedAccounts, actualAccounts);
    }

    [PostgreSqlFact]
    public async Task reset_is_deterministic()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await fixture.ResetCanonicalQueryDataAsync(connection);
        var first = await new NpgsqlCommand("SELECT \"Id\", \"Name\" FROM fg_query.\"Customer\" ORDER BY \"Id\"", connection)
            .ExecuteReaderAsync();
        var firstRows = new List<(int Id, string Name)>();
        await using (first)
        {
            while (await first.ReadAsync())
                firstRows.Add((first.GetInt32(0), first.GetString(1)));
        }

        await fixture.ResetCanonicalQueryDataAsync(connection);
        await using var second = new NpgsqlCommand("SELECT \"Id\", \"Name\" FROM fg_query.\"Customer\" ORDER BY \"Id\"", connection);
        await using var reader = await second.ExecuteReaderAsync();
        var secondRows = new List<(int Id, string Name)>();
        while (await reader.ReadAsync())
            secondRows.Add((reader.GetInt32(0), reader.GetString(1)));

        Assert.Equal(firstRows, secondRows);
    }
}
