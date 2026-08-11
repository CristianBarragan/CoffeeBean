using Foundgine.Abstractions;
using Foundgine.Planning;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.Planning.Tests;

public sealed class PlannerTests
{
    [Fact]
    public void Planner_creates_scan_for_root_and_traverses_for_children()
    {
        var graph = new SemanticGraph();
        var customer = graph.AddRoot(new EntityId(1), [new FieldId(1)]);
        var account = graph.Add(
            new EntityId(2),
            new RelationshipId(1),
            customer,
            [new FieldId(1)]);
        graph.Add(
            new EntityId(3),
            new RelationshipId(2),
            account,
            [new FieldId(1), new FieldId(3)]);

        var plan = new Planner().Plan(graph);

        Assert.NotSame(graph, plan);
        Assert.Equal(ExecutionOperation.Scan, plan.Root.Operation);
        Assert.Equal(new EntityId(1), plan.Root.EntityId);
        Assert.Equal(new[] { new FieldId(1) }, plan.Root.Fields);

        var accountPlan = Assert.Single(plan.Root.Children);
        Assert.Equal(ExecutionOperation.Traverse, accountPlan.Operation);
        Assert.Equal(new EntityId(2), accountPlan.EntityId);
        Assert.Equal(new RelationshipId(1), accountPlan.ViaRelationship);

        var transactionPlan = Assert.Single(accountPlan.Children);
        Assert.Equal(ExecutionOperation.Traverse, transactionPlan.Operation);
        Assert.Equal(new EntityId(3), transactionPlan.EntityId);
        Assert.Equal(new RelationshipId(2), transactionPlan.ViaRelationship);
        Assert.Equal(
            new[] { new FieldId(1), new FieldId(3) },
            transactionPlan.Fields);
    }

    [Fact]
    public void Planner_preserves_fan_out_without_flattening()
    {
        var graph = new SemanticGraph();
        var customer = graph.AddRoot(new EntityId(1));
        graph.Add(new EntityId(2), new RelationshipId(1), customer);
        graph.Add(new EntityId(4), new RelationshipId(3), customer);

        var plan = new Planner().Plan(graph);

        Assert.Equal(2, plan.Root.Children.Count);
        Assert.Contains(plan.Root.Children, node => node.EntityId == new EntityId(2));
        Assert.Contains(plan.Root.Children, node => node.EntityId == new EntityId(4));
    }

    [Fact]
    public void Planner_preserves_query_options_on_root()
    {
        var graph = new SemanticGraph
        {
            Options = new Foundgine.Semantics.Query.SemanticQueryOptions(
                Filter: null,
                Order: null,
                Limit: 10,
                After: null)
        };
        graph.AddRoot(new EntityId(1));

        var plan = new Planner().Plan(graph);

        Assert.NotNull(plan.Root.QueryOptions);
        Assert.Equal(10, plan.Root.QueryOptions!.Limit);
    }

    [Fact]
    public void Planner_rejects_empty_graph()
    {
        var graph = new SemanticGraph();

        var exception = Assert.Throws<InvalidOperationException>(() => new Planner().Plan(graph));

        Assert.Contains("empty semantic graph", exception.Message);
    }

    [Fact]
    public void Planner_rejects_multiple_roots()
    {
        var graph = new SemanticGraph();
        graph.AddRoot(new EntityId(1));
        graph.AddRoot(new EntityId(2));

        var exception = Assert.Throws<InvalidOperationException>(() => new Planner().Plan(graph));

        Assert.Contains("exactly one root", exception.Message);
    }


    [Fact]
    public void Execution_plan_contains_only_provider_independent_identity_and_logical_operations()
    {
        var graph = new SemanticGraph();
        graph.AddRoot(new EntityId(1), [new FieldId(1)]);

        var plan = new Planner().Plan(graph);

        Assert.Equal(new EntityId(1), plan.Root.EntityId);
        Assert.Equal(ExecutionOperation.Scan, plan.Root.Operation);
        Assert.Equal("Foundgine.Planning", plan.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Execution_plan_does_not_expose_metadata_types()
    {
        var planTypes = new[]
        {
            typeof(ExecutionPlan),
            typeof(ExecutionPlanNode)
        };

        foreach (var type in planTypes)
        {
            var exposedTypes = type
                .GetProperties()
                .Select(property => property.PropertyType)
                .Concat(type.GetConstructors()
                    .SelectMany(ctor => ctor.GetParameters().Select(parameter => parameter.ParameterType)))
                .Select(type => type.FullName ?? type.Name);

            Assert.DoesNotContain(exposedTypes, name => name.StartsWith("Foundgine.Metadata.EntityMetadata", StringComparison.Ordinal));
            Assert.DoesNotContain(exposedTypes, name => name.StartsWith("Foundgine.Metadata.RelationshipMetadata", StringComparison.Ordinal));
            Assert.DoesNotContain(exposedTypes, name => name.StartsWith("Foundgine.Metadata.ColumnReference", StringComparison.Ordinal));
        }
    }

}
