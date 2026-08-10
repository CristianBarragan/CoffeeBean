using Foundgine.Semantics;

namespace Foundgine.Planning;

public sealed class Planner : IPlanner
{
    public ExecutionPlan Plan(SemanticGraph graph) =>
        new(graph ?? throw new ArgumentNullException(nameof(graph)));
}
