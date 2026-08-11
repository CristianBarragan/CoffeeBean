using Foundgine.Abstractions;
using Foundgine.Execution;
using FoundgineExecutionContext = Foundgine.Execution.ExecutionContext;
using Foundgine.Generated;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Sql;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Foundgine.E2E.Tests;

public sealed class M34EvidenceTests
{
    [Fact]
    public async Task Sql_execution_returns_provider_neutral_evidence()
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
        var semanticGraph = new SemanticGraph();
        semanticGraph.AddRoot(
            new EntityId(3),
            new[] { new FieldId(1), new FieldId(3) },
            authorization.Predicate);

        var logicalPlan = new Planner().Plan(semanticGraph);
        var sqlPlan = new SqlCompiler(GeneratedMetadata.Registry).Compile(logicalPlan);

        var result = await new SqlExecutionProvider(connection).ExecuteAsync(
            sqlPlan,
            new FoundgineExecutionContext(new Dictionary<string, object?>
            {
                ["user.TenantId"] = 7
            }));

        Assert.NotNull(result.Evidence);
        var evidence = result.Evidence!;
        Assert.Equal("sql", evidence.Provider);
        Assert.False(string.IsNullOrWhiteSpace(evidence.PlanFingerprint));
        Assert.Contains(logicalPlan.Root.Id, evidence.AuthorizedNodeIds);
        Assert.Equal(1, evidence.RowsReturned);
        Assert.True(evidence.ElapsedMilliseconds >= 0);
        Assert.False(string.IsNullOrWhiteSpace(evidence.ProviderOperationFingerprint));
        Assert.NotEqual(sqlPlan.CommandText, evidence.ProviderOperationFingerprint);
    }

    [Fact]
    public async Task Equivalent_sql_plans_produce_the_same_plan_fingerprint()
    {
        var authorization = GeneratedMetadata.Registry.Authorizations.Single();

        var graph1 = new SemanticGraph();
        graph1.AddRoot(new EntityId(3), new[] { new FieldId(1), new FieldId(3) }, authorization.Predicate);
        var graph2 = new SemanticGraph();
        graph2.AddRoot(new EntityId(3), new[] { new FieldId(1), new FieldId(3) }, authorization.Predicate);

        var compiler = new SqlCompiler(GeneratedMetadata.Registry);
        var sql1 = compiler.Compile(new Planner().Plan(graph1));
        var sql2 = compiler.Compile(new Planner().Plan(graph2));

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE contracts (id INTEGER NOT NULL, contract_type INTEGER NOT NULL, tenant_id INTEGER NOT NULL);";
            await command.ExecuteNonQueryAsync();
        }

        var provider = new SqlExecutionProvider(connection);
        var context = new FoundgineExecutionContext(new Dictionary<string, object?> { ["user.TenantId"] = 7 });
        var first = await provider.ExecuteAsync(sql1, context);
        var second = await provider.ExecuteAsync(sql2, context);

        Assert.NotNull(first.Evidence);
        Assert.NotNull(second.Evidence);
        Assert.Equal(first.Evidence!.PlanFingerprint, second.Evidence!.PlanFingerprint);
    }
}
