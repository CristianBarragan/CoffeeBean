using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Generated;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Sql;
using Microsoft.Data.Sqlite;
using Xunit;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.E2E.Tests;

public sealed class M31AuthorizationSqlExecutionTests
{
    [Fact]
    public async Task Aot_authorization_predicate_is_lowered_and_enforced_by_sql_execution()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE contracts (id INTEGER NOT NULL, contract_type INTEGER NOT NULL, tenant_id INTEGER NOT NULL);";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "INSERT INTO contracts (id, contract_type, tenant_id) VALUES (1, 0, 7), (2, 1, 9);";
            await command.ExecuteNonQueryAsync();
        }

        var authorization = GeneratedMetadata.Registry.Authorizations.Single();
        var graph = new SemanticGraph();
        graph.AddRoot(new EntityId(3), new[] { new FieldId(1), new FieldId(3) }, authorization.Predicate);

        var plan = new Planner().Plan(graph);
        var sql = new SqlCompiler(GeneratedMetadata.Registry).Compile(plan);

        Assert.Contains("tenant_id", sql.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@auth", sql.CommandText, StringComparison.OrdinalIgnoreCase);

        var result = await new SqlExecutionProvider(connection).ExecuteAsync(
            sql,
            new ExecutionContext(new Dictionary<string, object?>
            {
                ["user.TenantId"] = 7
            }));

        Assert.Single(result.Rows);
        Assert.Equal(1L, result.Rows[0].Values["n0_Id"]);
    }
    [Fact]
    public async Task Missing_authorization_context_value_fails_closed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE contracts (id INTEGER NOT NULL, contract_type INTEGER NOT NULL, tenant_id INTEGER NOT NULL);";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "INSERT INTO contracts (id, contract_type, tenant_id) VALUES (1, 0, 7);";
            await command.ExecuteNonQueryAsync();
        }

        var authorization = GeneratedMetadata.Registry.Authorizations.Single();
        var graph = new SemanticGraph();
        graph.AddRoot(new EntityId(3), new[] { new FieldId(1), new FieldId(3) }, authorization.Predicate);
        var plan = new SqlCompiler(GeneratedMetadata.Registry).Compile(new Planner().Plan(graph));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqlExecutionProvider(connection).ExecuteAsync(plan, new ExecutionContext()));

        Assert.Contains("user.TenantId", exception.Message, StringComparison.Ordinal);
    }

}
