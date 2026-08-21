namespace Foundgine.Execution;

/// <summary>
/// Final execution boundary for security proofs. A provider plan is not
/// executable merely because it has been compiled; it must carry a satisfied
/// security-invariant proof produced by the provider compiler gate.
/// </summary>
public static class SecurityInvariantExecutionGate
{
    public static void EnsureExecutable(ProviderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var proof = plan.SecurityProof
            ?? throw new InvalidOperationException(
                $"Provider plan '{plan.GetType().Name}' has no security proof and cannot execute.");

        proof.EnsureSatisfied();
    }
}
