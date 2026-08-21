using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>A tamper-evident history record for one accepted proposer-credential lifecycle transition.</summary>
public sealed record AuthorizationRecoveryProposerCredentialAuditRecord(
    long AuditSequence,
    string ProposerId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryReconfigurationProposerCredentialState State,
    string PreviousRecordDigest,
    string RecordDigest,
    DateTimeOffset RecordedAtUtc);

public enum AuthorizationRecoveryProposerCredentialAuditVerificationOutcome
{
    Verified,
    Empty,
    SequenceGap,
    PreviousDigestMismatch,
    RecordDigestMismatch
}

public sealed record AuthorizationRecoveryProposerCredentialAuditVerificationResult(
    AuthorizationRecoveryProposerCredentialAuditVerificationOutcome Outcome,
    long? FailingSequence,
    string? Reason)
{
    public bool Verified => Outcome == AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.Verified;
}

/// <summary>
/// Hash-chained audit history for proposer credential registration, rotation and terminal lifecycle
/// transitions. It is deliberately separate from the authoritative credential store: the ledger is
/// evidence, not the trust root. Production deployments must persist it in independent append-only
/// or transparency storage if an attacker who compromises the control plane must not be able to erase history.
/// </summary>
public sealed class AuthorizationRecoveryProposerCredentialAuditLedger
{
    public const string GenesisPreviousDigest = AuthorizationRecoveryAnchorState.GenesisDigest;
    private readonly object _gate = new();
    private readonly List<AuthorizationRecoveryProposerCredentialAuditRecord> _records = new();

    public IReadOnlyList<AuthorizationRecoveryProposerCredentialAuditRecord> Records
    { get { lock (_gate) return _records.ToArray(); } }

    public AuthorizationRecoveryProposerCredentialAuditRecord? Head
    { get { lock (_gate) return _records.Count == 0 ? null : _records[^1]; } }

    public (long Sequence, string Digest) HeadState
    {
        get
        {
            lock (_gate)
                return _records.Count == 0
                    ? (0, GenesisPreviousDigest)
                    : (_records[^1].AuditSequence, _records[^1].RecordDigest);
        }
    }

    public AuthorizationRecoveryProposerCredentialAuditRecord Append(
        string proposerId,
        string credentialFingerprint,
        long credentialSequence,
        AuthorizationRecoveryReconfigurationProposerCredentialState state,
        DateTimeOffset? recordedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(proposerId)) throw new ArgumentException("Proposer ID is required.", nameof(proposerId));
        if (string.IsNullOrWhiteSpace(credentialFingerprint)) throw new ArgumentException("Credential fingerprint is required.", nameof(credentialFingerprint));
        if (credentialSequence <= 0) throw new ArgumentOutOfRangeException(nameof(credentialSequence));
        lock (_gate)
        {
            var expected = _records.Count == 0 ? 1 : _records[^1].AuditSequence + 1;
            var auditSequence = expected;
            var previous = _records.Count == 0 ? GenesisPreviousDigest : _records[^1].RecordDigest;
            var at = recordedAtUtc ?? DateTimeOffset.UtcNow;
            var digest = ComputeRecordDigest(auditSequence, proposerId, credentialFingerprint, credentialSequence, state, previous, at);
            var record = new AuthorizationRecoveryProposerCredentialAuditRecord(auditSequence, proposerId, credentialFingerprint, credentialSequence, state, previous, digest, at);
            _records.Add(record);
            return record;
        }
    }

    public static AuthorizationRecoveryProposerCredentialAuditLedger Restore(IReadOnlyList<AuthorizationRecoveryProposerCredentialAuditRecord> records)
    {
        var result = VerifyChain(records);
        if (!result.Verified) throw new AuthorizationRecoveryReconciliationException($"Cannot restore invalid proposer credential audit history: {result.Reason}");
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        lock (ledger._gate) ledger._records.AddRange(records);
        return ledger;
    }

    public AuthorizationRecoveryProposerCredentialAuditVerificationResult VerifyChain() => VerifyChain(Records);

    public static AuthorizationRecoveryProposerCredentialAuditVerificationResult VerifyChain(IReadOnlyList<AuthorizationRecoveryProposerCredentialAuditRecord> records)
    {
        if (records.Count == 0) return new(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.Empty, null, "The ledger has no records yet.");
        var previous = GenesisPreviousDigest;
        long expected = 1;
        foreach (var r in records)
        {
            if (r.AuditSequence != expected) return new(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.SequenceGap, r.AuditSequence, $"Expected audit sequence {expected}, found {r.AuditSequence}.");
            if (!string.Equals(r.PreviousRecordDigest, previous, StringComparison.OrdinalIgnoreCase)) return new(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.PreviousDigestMismatch, r.AuditSequence, "The record does not chain from its predecessor.");
            var computed = ComputeRecordDigest(r.AuditSequence, r.ProposerId, r.CredentialFingerprint, r.CredentialSequence, r.State, r.PreviousRecordDigest, r.RecordedAtUtc);
            if (!string.Equals(computed, r.RecordDigest, StringComparison.OrdinalIgnoreCase)) return new(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.RecordDigestMismatch, r.AuditSequence, "The record digest does not match its contents.");
            previous = r.RecordDigest;
            expected++;
        }
        return new(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.Verified, null, null);
    }

    /// <summary>
    /// Verifies that this ledger is exactly at the externally anchored head. An older valid
    /// ledger is rejected rather than accepted as a legitimate history after rollback.
    /// </summary>
    public async ValueTask VerifyAgainstAnchorAsync(
        IAuthorizationRecoveryProposerCredentialAuditHeadAnchor anchor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var verification = VerifyChain();
        if (!verification.Verified && verification.Outcome != AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.Empty)
            throw new AuthorizationRecoveryReconciliationException($"Cannot reconcile invalid proposer credential audit history: {verification.Reason}");

        var head = HeadState;
        var anchored = await anchor.ReadAsync(cancellationToken);
        if (head.Sequence < anchored.Sequence)
            throw new AuthorizationRecoveryProposerCredentialAuditHeadRollbackException(head.Sequence, head.Digest, anchored.Sequence, anchored.Digest);
        if (head.Sequence > anchored.Sequence)
            throw new AuthorizationRecoveryProposerCredentialAuditHeadRollbackException(head.Sequence, head.Digest, anchored.Sequence, anchored.Digest);
        if (!FixedEquals(head.Digest, anchored.Digest))
            throw new AuthorizationRecoveryProposerCredentialAuditHeadForkException(head.Sequence, head.Digest, anchored.Digest);
    }

    /// <summary>
    /// Reference append-and-anchor protocol. Durable production implementations should perform
    /// ledger persistence and anchor advancement in an atomic control-plane transaction. If the
    /// anchor CAS loses after persistence, this method fails closed; it never rolls history back.
    /// </summary>
    public async ValueTask<AuthorizationRecoveryProposerCredentialAuditRecord> AppendAndAnchorAsync(
        IAuthorizationRecoveryProposerCredentialAuditHeadAnchor anchor,
        string writerId,
        string proposerId,
        string credentialFingerprint,
        long credentialSequence,
        AuthorizationRecoveryReconfigurationProposerCredentialState state,
        DateTimeOffset? recordedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var anchored = await anchor.ReadAsync(cancellationToken);
        var current = HeadState;
        if (current.Sequence != anchored.Sequence || !FixedEquals(current.Digest, anchored.Digest))
            throw new AuthorizationRecoveryProposerCredentialAuditHeadForkException(current.Sequence, current.Digest, anchored.Digest);

        var record = Append(proposerId, credentialFingerprint, credentialSequence, state, recordedAtUtc);
        var advanced = await anchor.TryAdvanceAsync(
            anchored.Sequence,
            anchored.Digest,
            record.AuditSequence,
            record.RecordDigest,
            writerId,
            cancellationToken);
        if (!advanced)
            throw new AuthorizationRecoveryProposerCredentialAuditHeadRollbackException(
                record.AuditSequence, record.RecordDigest, anchored.Sequence, anchored.Digest);
        return record;
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(right.ToLowerInvariant()));

    private static string ComputeRecordDigest(long auditSequence, string proposerId, string fingerprint, long credentialSequence, AuthorizationRecoveryReconfigurationProposerCredentialState state, string previous, DateTimeOffset at)
    {
        var fields = new[] { auditSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), proposerId, fingerprint, credentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), state.ToString(), previous, at.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) };
        using var ms = new MemoryStream();
        foreach (var field in fields)
        {
            var bytes = Encoding.UTF8.GetBytes(field);
            ms.Write(BitConverter.GetBytes(bytes.Length));
            ms.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }
}
