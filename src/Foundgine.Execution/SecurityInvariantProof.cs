using Foundgine.Semantics.Security;

namespace Foundgine.Execution;

/// <summary>
/// Provider-facing attestation that the compiled plan preserves every security
/// invariant required by the semantic plan. This is a contract proof, not a
/// claim that the provider can make arbitrary business policy correct.
/// </summary>
public sealed record SecurityInvariantProof(
    string Provider,
    IReadOnlyList<string> Required,
    IReadOnlyList<string> Preserved,
    IReadOnlyList<string> Missing)
{
    public bool IsSatisfied => Missing.Count == 0;

    public void EnsureSatisfied()
    {
        if (!IsSatisfied)
            throw new InvalidOperationException(
                $"Provider '{Provider}' cannot satisfy required security invariants: {string.Join(", ", Missing)}.");
    }

    public static SecurityInvariantProof Create(
        string provider,
        IEnumerable<string> required,
        IEnumerable<string> preserved)
    {
        var requiredSet = required
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var preservedSet = preserved
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var missing = requiredSet
            .Except(preservedSet, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new SecurityInvariantProof(provider, requiredSet, preservedSet, missing);
    }
}

/// <summary>
/// Optional provider contract used by the execution gate. A provider declares
/// which canonical invariants its compiler preserves; the engine still checks
/// that every invariant required by the current plan is covered.
/// </summary>
public interface ISecurityInvariantProviderCompiler
{
    IReadOnlyCollection<string> PreservedSecurityInvariants { get; }
}

public static class SecurityInvariantProofGate
{
    public static ProviderPlan AttachAndValidate(
        ProviderPlan plan,
        ExecutionIR ir,
        IProviderPlanCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(compiler);

        var required = ir.RequiredSecurityInvariants;
        if (required.Count == 0)
            throw new InvalidOperationException(
                "ExecutionIR contains no security obligations. An executable provider plan must carry a non-empty security proof.");

        if (compiler is not ISecurityInvariantProviderCompiler securityCompiler)
        {
            throw new InvalidOperationException(
                $"Provider compiler '{compiler.GetType().Name}' does not declare a security-invariant preservation contract.");
        }

        foreach (var id in required)
        {
            if (!SecurityInvariantRegistry.Contains(id))
                throw new InvalidOperationException($"Unknown required security invariant '{id}'.");
        }

        var proof = SecurityInvariantProof.Create(
            plan.Provider,
            required,
            securityCompiler.PreservedSecurityInvariants);
        proof.EnsureSatisfied();

        // A declared preservation profile is the provider capability baseline.
        // If the provider also supplies an executable conformance evaluator,
        // certify the concrete compiled plan before attaching the proof.
        if (compiler is IProviderSecurityConformanceEvaluator evaluator)
        {
            var conformance = evaluator.Evaluate(ir, plan);
            conformance.EnsureSatisfied();

            var missing = required
                .Except(conformance.Satisfied, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"Provider '{plan.Provider}' executable conformance did not satisfy required invariants: {string.Join(", ", missing)}.");
        }

        return plan with { SecurityProof = proof };
    }
}
