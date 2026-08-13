using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.Planning;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.E2E.Tests;

public sealed class ResultModelSemanticsTests
{
    [Fact]
    public void Identity_is_not_required_in_the_projection_to_reconstruct_unique_nodes()
    {
        var customer = new EntityId(1);
        var id = new FieldId(1);
        var name = new FieldId(2);

        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e
                .Identity(id, "Id")
                .Field(id, "Id", typeof(long))
                .Field(name, "Name", typeof(string)))
            .Build();

        var plan = new ExecutionPlan(
            new ExecutionPlanNode(
                1,
                ExecutionOperation.Scan,
                customer,
                [name],
                null,
                []));

        ExecutionRow Row(long identity, string value) => new(
            new Dictionary<string, object?>(),
            new Dictionary<ExecutionCellKey, object?>
            {
                [new ExecutionCellKey(1, customer, id)] = identity,
                [new ExecutionCellKey(1, customer, name)] = value
            });

        var result = new ResultMaterializer(model).Materialize(
            plan,
            new ExecutionResult([Row(1, "Ada"), Row(2, "Grace"), Row(1, "Ada")]));

        Assert.Equal(2, result.Roots.Count);
        Assert.Equal(1L, result.Roots[0].IdentityValue);
        Assert.Equal(2L, result.Roots[1].IdentityValue);
        Assert.DoesNotContain(id, result.Roots[0].Values.Keys);
        Assert.Equal("Ada", result.Roots[0].Values[name]);
    }

    [Fact]
    public void Materialization_preserves_execution_page_info_and_evidence()
    {
        var customer = new EntityId(1);
        var id = new FieldId(1);
        var model = new SemanticModelBuilder()
            .Entity(customer, "Customer", e => e
                .Identity(id, "Id")
                .Field(id, "Id", typeof(long)))
            .Build();

        var plan = new ExecutionPlan(
            new ExecutionPlanNode(1, ExecutionOperation.Scan, customer, [id], null, []));

        var pageInfo = new ExecutionPageInfo("start", "end", true, false);
        var evidence = new ExecutionEvidence("sql", "plan", [1], 1, 3);
        var row = new ExecutionRow(
            new Dictionary<string, object?>(),
            new Dictionary<ExecutionCellKey, object?>
            {
                [new ExecutionCellKey(1, customer, id)] = 42L
            });

        var materialized = new ResultMaterializer(model).Materialize(
            plan,
            new ExecutionResult([row], pageInfo, evidence));

        Assert.Same(pageInfo, materialized.PageInfo);
        Assert.Same(evidence, materialized.Evidence);
    }
}
