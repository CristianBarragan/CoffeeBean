using Foundgine;
using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Intent.Json;
using Foundgine.Semantics.Authorization;
using Foundgine.Sql;
using Foundgine.E2E.Tests.Banking;
using Npgsql;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

/// <summary>
/// M17.1 provider gate: the same hostile-agent boundary exercised by the
/// black-box SQLite test is executed against PostgreSQL. The test is skipped
/// unless FOUNDGINE_POSTGRES_CONNECTION_STRING is configured.
/// </summary>
public sealed class M171PostgresBlackBoxTests
{
    [PostgreSqlFact]
    public async Task Authorized_agent_intent_reaches_postgres_without_cross_tenant_rows()
    {
        var connectionString = Environment.GetEnvironmentVariable("FOUNDGINE_POSTGRES_CONNECTION_STRING")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await PrepareAsync(connection);

        var engine = new FoundgineEngine(
            new FoundgineOptions
            {
                Model = BankingSemanticModel.Build(),
                AuthorizationPolicy = new TenantPolicy()
            },
            new SqlCompiler(BankingRelationalMetadata.Build()),
            new SqlExecutionProvider(connection));

        var intent = new JsonReadIntentAdapter().Parse("""
        {
          "rootEntity": "Customer",
          "selections": [
            { "field": "Id" },
            { "field": "Name" }
          ]
        }
        """);

        var result = await engine.ExecuteAsync(
            intent,
            new ExecutionContext(new Dictionary<string, object?>
            {
                ["user.TenantId"] = 7
            }));

        var row = Assert.Single(result.Rows);
        Assert.Equal(1L, row.Values["__fg_0_Id"]);
        Assert.Equal("Alice", row.Values["__fg_0_Name"]);
        Assert.NotNull(result.Evidence);
        Assert.NotNull(result.Receipt);
    }

    [PostgreSqlFact]
    public async Task SQL_injection_shaped_agent_value_is_parameterized_by_postgres_execution()
    {
        var connectionString = Environment.GetEnvironmentVariable("FOUNDGINE_POSTGRES_CONNECTION_STRING")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await PrepareAsync(connection);

        var engine = new FoundgineEngine(
            new FoundgineOptions
            {
                Model = BankingSemanticModel.Build(),
                AuthorizationPolicy = new TenantPolicy()
            },
            new SqlCompiler(BankingRelationalMetadata.Build()),
            new SqlExecutionProvider(connection));

        var intent = new JsonReadIntentAdapter().Parse("""
        {
          "rootEntity": "Customer",
          "selections": [
            { "field": "Id" },
            { "field": "Name" }
          ],
          "filter": {
            "kind": "field",
            "field": "Name",
            "operator": "Eq",
            "value": "' OR 1=1 --"
          }
        }
        """);

        var result = await engine.ExecuteAsync(
            intent,
            new ExecutionContext(new Dictionary<string, object?>
            {
                ["user.TenantId"] = 7
            }));

        Assert.Empty(result.Rows);
    }

    private static async Task PrepareAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
        CREATE TEMP TABLE "Customer" (
            "Id" bigint PRIMARY KEY,
            "Name" text NOT NULL,
            "TenantId" integer NOT NULL
        );
        INSERT INTO "Customer" ("Id", "Name", "TenantId")
        VALUES (1, 'Alice', 7), (2, 'Bob', 8);
        """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TenantPolicy : AllowAllSemanticAuthorizationPolicy
    {
        public override AuthorizationPredicate? GetPredicate(
            Foundgine.Abstractions.EntityId entityId,
            AuthorizationOperation operation) =>
            operation == AuthorizationOperation.Read && entityId == BankingSemanticModel.Customer
                ? AuthorizationPredicate.Equal(
                    AuthorizationPredicate.Member(
                        AuthorizationPredicate.ResourceParameter("resource"), "TenantId"),
                    AuthorizationPredicate.Member(
                        AuthorizationPredicate.ContextParameter("user"), "TenantId"))
                : null;
    }
}
