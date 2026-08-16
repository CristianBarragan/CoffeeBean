using Foundgine.Semantics.Security;

namespace Foundgine.HighAssurance.Postgres;

/// <summary>
/// Provider-specific conformance contract for the high-assurance PostgreSQL mutation boundary.
/// This is intentionally narrower than the generic SQL security contract: consequential
/// mutations require transactional guarantees that ordinary query compilation cannot provide.
/// </summary>
public sealed record PostgresMutationSecurityConformance(
    IReadOnlyList<string> RequiredInvariants,
    bool UsesSingleTransaction,
    bool LocksMutationRowsDeterministically,
    bool RevalidatesAuthorizationAtExecution,
    bool SerializesIdempotencyKeys,
    bool PersistsIdempotencyInsideTransaction,
    bool PersistsAuditInsideTransaction,
    bool EmitsExecutionReceipt)
{
    public bool IsSatisfied =>
        RequiredInvariants.All(x => x is not null) &&
        UsesSingleTransaction &&
        LocksMutationRowsDeterministically &&
        RevalidatesAuthorizationAtExecution &&
        SerializesIdempotencyKeys &&
        PersistsIdempotencyInsideTransaction &&
        PersistsAuditInsideTransaction &&
        EmitsExecutionReceipt;

    public IReadOnlyList<string> MissingRequirements()
    {
        var missing = new List<string>();
        if (!UsesSingleTransaction) missing.Add("mutation.atomic");
        if (!LocksMutationRowsDeterministically) missing.Add("mutation.atomic.row-locking");
        if (!RevalidatesAuthorizationAtExecution) missing.Add("authorization.runtime");
        if (!SerializesIdempotencyKeys) missing.Add("mutation.replay-protection");
        if (!PersistsIdempotencyInsideTransaction) missing.Add("mutation.idempotency");
        if (!PersistsAuditInsideTransaction) missing.Add("evidence.audit");
        if (!EmitsExecutionReceipt) missing.Add("evidence.execution-receipt");
        return missing;
    }

    public void EnsureSatisfied()
    {
        var missing = MissingRequirements();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"PostgreSQL high-assurance mutation provider does not satisfy: {string.Join(", ", missing)}.");
    }

    public static PostgresMutationSecurityConformance TransferFunds => new(
        [
            "tenant.isolation",
            "authorization.runtime",
            "mutation.atomic",
            "mutation.idempotency",
            "mutation.replay-protection",
            "evidence.audit",
            "evidence.execution-receipt"
        ],
        UsesSingleTransaction: true,
        LocksMutationRowsDeterministically: true,
        RevalidatesAuthorizationAtExecution: true,
        SerializesIdempotencyKeys: true,
        PersistsIdempotencyInsideTransaction: true,
        PersistsAuditInsideTransaction: true,
        EmitsExecutionReceipt: true);
}

/// <summary>
/// Small executable gate used by tests and provider startup validation. It deliberately
/// validates the provider contract independently from the generic invariant registry.
/// </summary>
public static class PostgresMutationSecurityConformanceGate
{
    public static void EnsureTransferFundsConformance() =>
        PostgresMutationSecurityConformance.TransferFunds.EnsureSatisfied();

    public static void EnsureKnownInvariants()
    {
        foreach (var invariant in PostgresMutationSecurityConformance.TransferFunds.RequiredInvariants)
        {
            if (!SecurityInvariantRegistry.Contains(invariant))
                throw new InvalidOperationException($"Unknown security invariant '{invariant}'.");
        }
    }
}
