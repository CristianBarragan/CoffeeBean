using Foundgine.Core.Execution;
using Foundgine.Core.Semantic.Planning;

namespace Foundgine.Testing;

public static class ExecutionIRTestFactory
{
    public static ExecutionIR Create(
        ExecutionIRNode root,
        IReadOnlyList<string> requiredSecurityInvariants)
    {
        return new ExecutionIR(
            root,
            requiredSecurityInvariants,
            new SemanticPlanAuthorizationBinding(
                "test-contract",
                "test-authorization"));
    }
}