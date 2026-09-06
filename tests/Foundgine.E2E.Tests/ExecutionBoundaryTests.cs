using Foundgine.Core.Abstractions;
using Foundgine.Core.Execution;
using Foundgine.Core.Semantic;
using Foundgine.Core.Semantic.Planning;

namespace Foundgine.E2E.Tests;

public sealed class ExecutionBoundaryTests
{
    [Fact]
    public void Execution_assembly_does_not_reference_metadata_directly()
    {
        var references = typeof(ExecutionResult).Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Foundgine.Core.Semantic.Metadata", references);
    }

    [Fact]
    public void Result_materialization_uses_semantic_topology_not_storage_metadata()
    {
        var model = new SemanticModelBuilder()
            .Entity(new EntityId(1), "Customer", entity => entity
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(1), "Id", typeof(long))
                .Field(new FieldId(2), "Name", typeof(string)))
            .Build();

        var plan = new SemanticPlan(
            new SemanticPlanNode(
                1,
                ExecutionOperation.Scan,
                new EntityId(1),
                [new FieldId(1), new FieldId(2)],
                null,
                []));

        var row = new ExecutionRow(
            new Dictionary<string, object?>(),
            new Dictionary<ExecutionCellKey, object?>
            {
                [new ExecutionCellKey(1, new EntityId(1), new FieldId(1))] = 42L,
                [new ExecutionCellKey(1, new EntityId(1), new FieldId(2))] = "Ada"
            });

        var materialized = new ResultMaterializer(model).Materialize(
            plan,
            new ExecutionResult([row]));

        var root = Assert.Single(materialized.Roots);
        Assert.Equal(42L, root.Values[new FieldId(1)]);
        Assert.Equal("Ada", root.Values[new FieldId(2)]);
    }
}