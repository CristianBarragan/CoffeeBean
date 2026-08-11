using Foundgine.Semantics;

namespace Foundgine.Planning;

public interface IPlanner
{
    ExecutionPlan Plan(SemanticGraph graph);
}
