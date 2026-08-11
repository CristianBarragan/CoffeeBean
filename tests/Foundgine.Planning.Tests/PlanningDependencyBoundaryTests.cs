using Foundgine.Abstractions;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Query;
using Xunit;

namespace Foundgine.Planning.Tests;

public sealed class PlanningDependencyBoundaryTests
{
    [Fact]
    public void Planner_consumes_semantic_graph_without_physical_metadata()
    {
        var graph = new SemanticGraph
        {
            Options = new SemanticQueryOptions(Limit: 5)
        };
        graph.AddRoot(new EntityId(1), [new FieldId(1)]);

        var plan = new Planner().Plan(graph);

        Assert.Equal(new EntityId(1), plan.Root.EntityId);
        Assert.Equal(5, plan.Root.QueryOptions!.Limit);
    }

    [Fact]
    public void Execution_plan_public_contract_contains_no_metadata_types()
    {
        var publicTypes = new[]
        {
            typeof(ExecutionPlan),
            typeof(ExecutionPlanNode)
        };

        foreach (var type in publicTypes)
        {
            var members = type.GetProperties()
                .Select(x => x.PropertyType)
                .Concat(type.GetConstructors().SelectMany(x => x.GetParameters().Select(p => p.ParameterType)))
                .SelectMany(FlattenTypes)
                .ToArray();

            Assert.DoesNotContain(members, x => x.Namespace?.StartsWith("Foundgine.Metadata", StringComparison.Ordinal) == true);
        }
    }

    private static IEnumerable<Type> FlattenTypes(Type type)
    {
        yield return type;
        if (!type.IsGenericType)
            yield break;

        foreach (var argument in type.GetGenericArguments())
        foreach (var nested in FlattenTypes(argument))
            yield return nested;
    }
}
