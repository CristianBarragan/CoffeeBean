namespace Foundgine.Testing;

public static class ExecutionIRTestFactory
{
    public static Foundgine.Execution.ExecutionIR Create(
        Foundgine.Execution.ExecutionIRNode root,
        IReadOnlyList<string> requiredSecurityInvariants)
    {
        return new Foundgine.Execution.ExecutionIR(
            root,
            requiredSecurityInvariants,
            new Foundgine.Planning.SemanticPlanAuthorizationBinding(
                "test-contract",
                "test-authorization"));
    }
}