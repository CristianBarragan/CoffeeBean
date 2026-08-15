using Foundgine.Semantics;
using Foundgine.Semantics.IR;

namespace Foundgine.Planning;

public interface IPlanner
{
    SemanticPlan Plan(SemanticOperation operation);
    SemanticPlan Plan(SemanticGraph graph);
}
