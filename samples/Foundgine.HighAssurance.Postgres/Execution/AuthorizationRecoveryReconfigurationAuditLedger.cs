using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>
/// One entry in the reconfiguration audit ledger. Each record commits to the configuration it
/// describes (<see cref="ConfigVersion"/>, <see cref="MembershipDigest"/>), the digest of the record
/// immediately before it, and its own digest — the same hash-chaining discipline the recovery anchor
/// itself already uses for its sequence/digest history, now applied to the history of *who was allowed
/// to vote*, not just to the recovery state those votes protect.
/// </summary>
public sealed record AuthorizationRecoveryReconfigurationAuditRecord(
    long ConfigVersion,
    string MembershipDigest,
    string PreviousRecordDigest,
    string RecordDigest,
    string? ProposerId,
    DateTimeOffset RecordedAtUtc);

public enum AuthorizationRecoveryLedgerVerificationOutcome
{
    Verified,
    Empty,

    /// <summary>Config versions in the ledger are not exactly consecutive from the first record.</summary>
    VersionGap,

    /// <summary>A record's <see cref="AuthorizationRecoveryReconfigurationAuditRecord.PreviousRecordDigest"/>
    /// does not match the digest of the record before it (or the genesis digest, for the first record).</summary>
    PreviousDigestMismatch,

    /// <summary>A record's own digest does not match what its contents recompute to — the record was
    /// altered after being appended.</summary>
    RecordDigestMismatch
}

public sealed record AuthorizationRecoveryLedgerVerificationResult(
    AuthorizationRecoveryLedgerVerificationOutcome Outcome,
    long? FailingVersion,
    string? Reason)
{
    public bool Verified => Outcome == AuthorizationRecoveryLedgerVerificationOutcome.Verified;

    public static AuthorizationRecoveryLedgerVerificationResult Success() =>
        new(AuthorizationRecoveryLedgerVerificationOutcome.Verified, null, null);

    public static AuthorizationRecoveryLedgerVerificationResult Empty() =>
        new(AuthorizationRecoveryLedgerVerificationOutcome.Empty, null, "The ledger has no records yet.");

    public static AuthorizationRecoveryLedgerVerificationResult VersionGap(long expected, long found) =>
        new(
            AuthorizationRecoveryLedgerVerificationOutcome.VersionGap,
            found,
            $"Expected the next ledger record to be config version {expected}, but found {found}; a record was skipped, duplicated, or reordered.");

    public static AuthorizationRecoveryLedgerVerificationResult PreviousDigestMismatch(long version) =>
        new(
            AuthorizationRecoveryLedgerVerificationOutcome.PreviousDigestMismatch,
            version,
            $"The record for config version {version} does not chain from the digest of the record before it.");

    public static AuthorizationRecoveryLedgerVerificationResult RecordDigestMismatch(long version) =>
        new(
            AuthorizationRecoveryLedgerVerificationOutcome.RecordDigestMismatch,
            version,
            $"The record for config version {version} does not recompute to its own stated digest; it was altered after being appended.");
}

/// <summary>
/// Tamper-evident, hash-chained append-only ledger of every witness-set reconfiguration a
/// <see cref="ReconfigurableAuthorizationRecoveryQuorumAnchor"/> has ever accepted, including its
/// initial (genesis) membership.
///
/// M5.39 made reconfiguration itself require a reachable majority of the current witnesses, closing
/// off minority-driven and stale-configuration attacks against the *live* membership. It explicitly
/// did not attempt to make reconfiguration history auditable — it left that to "the same control
/// plane that operates the witnesses themselves." M5.40 provides that piece: a caller who only ever
/// sees the *current* configuration (as M5.39 correctly restricts them to) can still independently
/// verify the entire sequence of memberships that led to it, and detect if that history was ever
/// altered, truncated, or reordered.
///
/// Each record commits to: its config version, a digest of the membership it introduced, the digest
/// of the record before it, and its own digest over all of that. <see cref="VerifyChain(IReadOnlyList{AuthorizationRecoveryReconfigurationAuditRecord})"/>
/// is a static, dependency-free function of the records alone, so it can verify a ledger reconstructed
/// from durable storage exactly as it verifies the live in-process ledger — the live ledger is a
/// convenience, not a trust root.
///
/// What this deliberately does not attempt: chain math alone cannot stop an attacker who can silently
/// overwrite every copy of the ledger, including whatever durable store holds it — that is the same
/// limit M5.39 already stated for the witnesses themselves. Production deployments must persist every
/// appended record to an append-only, tamper-evident store (e.g. a WORM object store or an external
/// transparency log) rather than relying on this in-process list as the only copy.
/// </summary>
public sealed class AuthorizationRecoveryReconfigurationLedger
{
    /// <summary>The previous-record digest expected on the very first ledger entry.</summary>
    public const string GenesisPreviousDigest = AuthorizationRecoveryAnchorState.GenesisDigest;

    private readonly object _gate = new();
    private readonly List<AuthorizationRecoveryReconfigurationAuditRecord> _records = new();

    /// <summary>Snapshot of every record appended so far, oldest first.</summary>
    public IReadOnlyList<AuthorizationRecoveryReconfigurationAuditRecord> Records
    {
        get { lock (_gate) return _records.ToArray(); }
    }

    public AuthorizationRecoveryReconfigurationAuditRecord Append(
        long configVersion,
        IReadOnlyList<AuthorizationRecoveryQuorumWitness> membership,
        string? proposerId)
    {
        lock (_gate)
        {
            var previousDigest = _records.Count == 0 ? GenesisPreviousDigest : _records[^1].RecordDigest;
            var record = BuildRecord(configVersion, membership, previousDigest, proposerId, DateTimeOffset.UtcNow);
            _records.Add(record);
            return record;
        }
    }

    /// <summary>Reconstructs an in-memory ledger from already verified durable records.</summary>
    public static AuthorizationRecoveryReconfigurationLedger Restore(
        IReadOnlyList<AuthorizationRecoveryReconfigurationAuditRecord> records)
    {
        var verification = VerifyChain(records);
        if (!verification.Verified)
            throw new AuthorizationRecoveryReconciliationException(
                $"Cannot restore an invalid reconfiguration ledger: {verification.Reason}");

        var ledger = new AuthorizationRecoveryReconfigurationLedger();
        lock (ledger._gate)
        {
            ledger._records.AddRange(records);
        }
        return ledger;
    }

    /// <summary>Verifies the ledger's own current records. Equivalent to <c>VerifyChain(Records)</c>.</summary>
    public AuthorizationRecoveryLedgerVerificationResult VerifyChain() => VerifyChain(Records);

    /// <summary>
    /// Verifies an arbitrary sequence of records — live, reconstructed from durable storage, or
    /// supplied by a suspicious counterparty — with no dependency on any particular ledger instance.
    /// </summary>
    public static AuthorizationRecoveryLedgerVerificationResult VerifyChain(
        IReadOnlyList<AuthorizationRecoveryReconfigurationAuditRecord> records)
    {
        if (records.Count == 0)
            return AuthorizationRecoveryLedgerVerificationResult.Empty();

        var expectedPrevious = GenesisPreviousDigest;
        long? expectedVersion = null;

        foreach (var record in records)
        {
            if (expectedVersion is not null && record.ConfigVersion != expectedVersion)
                return AuthorizationRecoveryLedgerVerificationResult.VersionGap(expectedVersion.Value, record.ConfigVersion);

            if (!string.Equals(record.PreviousRecordDigest, expectedPrevious, StringComparison.OrdinalIgnoreCase))
                return AuthorizationRecoveryLedgerVerificationResult.PreviousDigestMismatch(record.ConfigVersion);

            var recomputed = ComputeRecordDigest(
                record.ConfigVersion, record.MembershipDigest, record.PreviousRecordDigest, record.ProposerId, record.RecordedAtUtc);
            if (!string.Equals(recomputed, record.RecordDigest, StringComparison.OrdinalIgnoreCase))
                return AuthorizationRecoveryLedgerVerificationResult.RecordDigestMismatch(record.ConfigVersion);

            expectedPrevious = record.RecordDigest;
            expectedVersion = record.ConfigVersion + 1;
        }

        return AuthorizationRecoveryLedgerVerificationResult.Success();
    }

    /// <summary>
    /// Deterministic digest of a membership's witness ids alone (sorted, so record order among the
    /// caller's list never affects the digest). Only ids are committed to — a witness's reachability
    /// callback and backing anchor are runtime-local behavior, not part of the durable audit record.
    /// </summary>
    public static string ComputeMembershipDigest(IReadOnlyList<AuthorizationRecoveryQuorumWitness> membership)
        => ComputeMembershipDigest(membership.Select(static w => w.WitnessId).ToArray());

    public static string ComputeMembershipDigest(IReadOnlyList<string> witnessIds)
    {
        var ids = witnessIds.OrderBy(static id => id, StringComparer.Ordinal);
        var joined = string.Join('|', ids);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }

    private static AuthorizationRecoveryReconfigurationAuditRecord BuildRecord(
        long configVersion,
        IReadOnlyList<AuthorizationRecoveryQuorumWitness> membership,
        string previousDigest,
        string? proposerId,
        DateTimeOffset recordedAtUtc)
    {
        var membershipDigest = ComputeMembershipDigest(membership);
        var recordDigest = ComputeRecordDigest(configVersion, membershipDigest, previousDigest, proposerId, recordedAtUtc);
        return new AuthorizationRecoveryReconfigurationAuditRecord(
            configVersion, membershipDigest, previousDigest, recordDigest, proposerId, recordedAtUtc);
    }

    private static string ComputeRecordDigest(
        long configVersion, string membershipDigest, string previousDigest, string? proposerId, DateTimeOffset recordedAtUtc)
    {
        var payload =
            $"{configVersion}|{membershipDigest}|{previousDigest}|{proposerId ?? string.Empty}|{recordedAtUtc.ToUnixTimeMilliseconds()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
