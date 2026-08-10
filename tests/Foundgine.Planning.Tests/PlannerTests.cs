using Foundgine.Planning;
using Foundgine.Semantics;
using Xunit;

namespace Foundgine.Planning.Tests;

public sealed class PlannerTests
{
    [Fact]
    public void Planner_preserves_the_semantic_graph_as_provider_independent_plan()
    {
        var graph = new SemanticGraph();
        graph.AddRoot(new Foundgine.Metadata.EntityId(1));

        var plan = new Planner().Plan(graph);

        Assert.Same(graph, plan.Graph);
    }
}
