using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>Durable witness-credential lifecycle event replicated between control-plane instances.</summary>
public sealed record AuthorizationRecoveryWitnessCredentialLifecycleRecord(
    long Revision,
    string WitnessId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryWitnessCredentialState State,
    string PreviousDigest,
    string Digest);

public sealed record AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage(
    IReadOnlyList<AuthorizationRecoveryWitnessCredentialLifecycleRecord> Records,
    string HeadDigest)
{
    public static AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage Empty =>
        new(Array.Empty<AuthorizationRecoveryWitnessCredentialLifecycleRecord>(),
            AuthorizationRecoveryWitnessCredentialLifecycleReplication.GenesisDigest);
}

public enum AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult
{
    Applied,
    AlreadyApplied,
    Gap,
    PreviousDigestMismatch,
    DivergentRevision,
    InvalidRecord,
    InvalidHistory,
    StaleRecovery
}

/// <summary>
/// Replicated witness credential lifecycle journal. The journal is deliberately
/// separate from credential secrets: only identity, fingerprint, generation and
/// lifecycle state cross the replication boundary.
/// </summary>
public sealed class AuthorizationRecoveryWitnessCredentialLifecycleReplication
{
    public const string GenesisDigest = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly object _gate = new();
    private readonly List<AuthorizationRecoveryWitnessCredentialLifecycleRecord> _records = new();

    public long Revision { get { lock (_gate) return _records.Count; } }
    public string HeadDigest { get { lock (_gate) return _records.Count == 0 ? GenesisDigest : _records[^1].Digest; } }

    public AuthorizationRecoveryWitnessCredentialLifecycleRecord Append(
        string witnessId,
        string credentialFingerprint,
        long credentialSequence,
        AuthorizationRecoveryWitnessCredentialState state)
    {
        ValidateFields(witnessId, credentialFingerprint, credentialSequence);
        lock (_gate)
        {
            var revision = _records.Count + 1L;
            var previous = _records.Count == 0 ? GenesisDigest : _records[^1].Digest;
            var digest = ComputeDigest(revision, witnessId, credentialFingerprint, credentialSequence, state, previous);
            var record = new AuthorizationRecoveryWitnessCredentialLifecycleRecord(
                revision, witnessId, credentialFingerprint, credentialSequence, state, previous, digest);
            _records.Add(record);
            return record;
        }
    }

    public AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult Apply(
        AuthorizationRecoveryWitnessCredentialLifecycleRecord record)
    {
        if (!ValidateRecord(record))
            return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidRecord;

        lock (_gate)
        {
            var expectedRevision = _records.Count + 1L;
            var currentDigest = _records.Count == 0 ? GenesisDigest : _records[^1].Digest;

            if (record.Revision <= _records.Count)
            {
                var existing = _records[(int)record.Revision - 1];
                return string.Equals(existing.Digest, record.Digest, StringComparison.OrdinalIgnoreCase)
                    ? AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.AlreadyApplied
                    : AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.DivergentRevision;
            }

            if (record.Revision != expectedRevision)
                return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Gap;

            if (!string.Equals(record.PreviousDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
                return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.PreviousDigestMismatch;

            _records.Add(record);
            return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied;
        }
    }

    public AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage ExportRecoveryPackage()
    {
        lock (_gate)
        {
            return new(_records.ToArray(), _records.Count == 0 ? GenesisDigest : _records[^1].Digest);
        }
    }

    public AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult Recover(
        AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage package)
    {
        if (package is null)
            return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidHistory;

        lock (_gate)
        {
            // Validate the complete package before mutating the journal. Recovery is
            // therefore atomic with respect to malformed, truncated, or tampered
            // packages: a failed package can never leave a partially recovered head.
            var expectedPrevious = _records.Count == 0 ? GenesisDigest : _records[^1].Digest;
            var expectedRevision = _records.Count + 1L;
            var pending = new List<AuthorizationRecoveryWitnessCredentialLifecycleRecord>();

            foreach (var record in package.Records)
            {
                if (!ValidateRecord(record))
                    return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidHistory;

                if (record.Revision < expectedRevision)
                {
                    if (record.Revision <= _records.Count &&
                        string.Equals(_records[(int)record.Revision - 1].Digest, record.Digest, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.DivergentRevision;
                }

                if (record.Revision != expectedRevision)
                    return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Gap;

                if (!string.Equals(record.PreviousDigest, expectedPrevious, StringComparison.OrdinalIgnoreCase))
                    return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.PreviousDigestMismatch;

                pending.Add(record);
                expectedPrevious = record.Digest;
                expectedRevision++;
            }

            if (!string.Equals(package.HeadDigest, expectedPrevious, StringComparison.OrdinalIgnoreCase))
                return AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidHistory;

            foreach (var record in pending)
                _records.Add(record);

            return package.Records.Count == 0
                ? AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.AlreadyApplied
                : AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied;
        }
    }

    public IReadOnlyList<AuthorizationRecoveryWitnessCredentialLifecycleRecord> ReadAll()
    {
        lock (_gate) return _records.ToArray();
    }

    public static string ComputeDigest(
        long revision,
        string witnessId,
        string credentialFingerprint,
        long credentialSequence,
        AuthorizationRecoveryWitnessCredentialState state,
        string previousDigest)
    {
        var payload = string.Join("|", revision, witnessId, credentialFingerprint,
            credentialSequence, state, previousDigest);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static bool ValidateRecord(AuthorizationRecoveryWitnessCredentialLifecycleRecord record)
    {
        if (record.Revision <= 0 || string.IsNullOrWhiteSpace(record.WitnessId) ||
            string.IsNullOrWhiteSpace(record.CredentialFingerprint) || record.CredentialSequence <= 0 ||
            string.IsNullOrWhiteSpace(record.PreviousDigest) || string.IsNullOrWhiteSpace(record.Digest))
            return false;

        var expected = ComputeDigest(record.Revision, record.WitnessId, record.CredentialFingerprint,
            record.CredentialSequence, record.State, record.PreviousDigest);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(record.Digest));
    }

    private static void ValidateFields(string witnessId, string fingerprint, long sequence)
    {
        if (string.IsNullOrWhiteSpace(witnessId)) throw new ArgumentException("Witness ID is required.", nameof(witnessId));
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("Credential fingerprint is required.", nameof(fingerprint));
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
    }
}
