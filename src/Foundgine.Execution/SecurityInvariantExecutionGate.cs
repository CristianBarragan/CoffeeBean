namespace Foundgine.Execution;

/// <summary>
/// Final execution boundary. A provider plan must carry a satisfied security
/// certificate issued for this exact plan and this exact Execution IR.
/// </summary>
public static class SecurityInvariantExecutionGate
{
    public static void EnsureExecutable(ProviderPlan plan, ExecutionIR ir)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ir);

        var proof = plan.SecurityProof
            ?? throw new InvalidOperationException(
                $"Provider plan '{plan.GetType().Name}' has no security certificate and cannot execute.");

        if (!string.Equals(proof.Provider, plan.Provider, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Security certificate provider '{proof.Provider}' does not match provider plan '{plan.Provider}'.");

        if (!proof.IsBoundTo(plan, ir))
            throw new InvalidOperationException(
                "Security certificate is not bound to the exact provider plan and Execution IR being executed.");

        proof.EnsureSatisfied();
    }

    [Obsolete("Use EnsureExecutable(plan, ir) so the certificate is bound to the exact Execution IR.")]
    public static void EnsureExecutable(ProviderPlan plan) =>
        throw new InvalidOperationException(
            "Execution IR is required to validate the security certificate binding.");
}
