using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>
/// M5.73 source-authenticated replication envelope for proposer credential lifecycle state.
/// The envelope is ordered by authority epoch, lifecycle sequence and previous digest.
/// </summary>
public sealed record AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope(
    string ProposerId,
    string CredentialId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryRepairProposerCredentialState State,
    long AuthorityEpoch,
    string SourceInstanceId,
    string SourceKeyId,
    long PreviousSequence,
    string PreviousDigest,
    string StateDigest,
    string IntegrityProof);

public enum AuthorizationRecoverySourceTrustKeyStatus
{
    Active,
    VerificationOnly,
    Revoked
}

public sealed record AuthorizationRecoverySourceTrustKey(
    string KeyId,
    AuthorizationRecoverySourceTrustKeyStatus Status,
    int Version,
    byte[] KeyMaterial);

public enum AuthorizationRecoverySourceTrustKeyLifecycleResult
{
    Activated,
    AlreadyActive,
    NotFound,
    CannotActivateRevokedKey,
    StaleRotation,
    ConflictingRotation,
    Revoked,
    AlreadyRevoked,
    CannotRevokeActiveKey
}

public enum AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult
{
    Applied,
    Duplicate,
    InvalidIntegrity,
    UntrustedSource,
    RevokedSourceKey,
    UnknownSourceKey,
    AuthorityEpochMismatch,
    SequenceGap,
    SequenceRollback,
    PreviousDigestMismatch,
    DivergentState
}

/// <summary>
/// M5.72 integrity and ordering boundary. Replication messages are accepted only
/// when authenticated by the replication trust key, belong to the current authority
/// epoch, advance exactly one lifecycle sequence, and chain to the current state digest.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity
{
    private readonly object _gate = new();
    private readonly byte[] _replicationKey;
    private string _localSourceKeyId = "source-key-v1";
    private int _localSourceKeyVersion = 1;
    private readonly Dictionary<string, Dictionary<string, AuthorizationRecoverySourceTrustKey>> _trustedSourceKeys = new(StringComparer.Ordinal);
    private readonly string _instanceId;
    private long _authorityEpoch;
    private readonly Dictionary<string, AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope> _states = new(StringComparer.Ordinal);

    public AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity(
        string instanceId,
        long authorityEpoch,
        ReadOnlySpan<byte> replicationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (authorityEpoch < 1) throw new ArgumentOutOfRangeException(nameof(authorityEpoch));
        if (replicationKey.Length < 16) throw new ArgumentException("Replication key must be at least 128 bits.", nameof(replicationKey));

        _instanceId = instanceId;
        _authorityEpoch = authorityEpoch;
        _replicationKey = replicationKey.ToArray();
        // The local key authenticates envelopes created by this instance.
        // Remote source keys must be explicitly trusted before Apply can accept them.
        _trustedSourceKeys[instanceId] = new Dictionary<string, AuthorizationRecoverySourceTrustKey>(StringComparer.Ordinal)
        {
            [_localSourceKeyId] = new(_localSourceKeyId, AuthorizationRecoverySourceTrustKeyStatus.Active, _localSourceKeyVersion, _replicationKey.ToArray())
        };
    }

    /// <summary>
    /// Registers the dedicated replication trust key for a remote source instance.
    /// Source identity is cryptographically bound to this key; an envelope cannot
    /// simply rewrite SourceInstanceId and remain valid.
    /// </summary>
    public void TrustSourceInstance(string sourceInstanceId, ReadOnlySpan<byte> replicationKey)
        => TrustSourceKey(sourceInstanceId, "source-key-v1", 1, replicationKey);

    public void TrustSourceKey(string sourceInstanceId, string keyId, int version, ReadOnlySpan<byte> replicationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (replicationKey.Length < 16)
            throw new ArgumentException("Replication key must be at least 128 bits.", nameof(replicationKey));

        lock (_gate)
        {
            if (sourceInstanceId == _instanceId)
                throw new InvalidOperationException("The local instance cannot be registered as a remote source.");
            if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys))
                _trustedSourceKeys[sourceInstanceId] = keys = new(StringComparer.Ordinal);
            keys[keyId] = new(keyId, AuthorizationRecoverySourceTrustKeyStatus.Active, version, replicationKey.ToArray());
        }
    }

    public AuthorizationRecoverySourceTrustKeyLifecycleResult RotateTrustedSourceKey(
        string sourceInstanceId, string expectedActiveKeyId, string newKeyId, int newVersion, ReadOnlySpan<byte> newKeyMaterial)
    {
        if (newVersion < 1) throw new ArgumentOutOfRangeException(nameof(newVersion));
        if (newKeyMaterial.Length < 16) throw new ArgumentException("Replication key must be at least 128 bits.", nameof(newKeyMaterial));
        lock (_gate)
        {
            if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys) || !keys.TryGetValue(expectedActiveKeyId, out var current))
                return AuthorizationRecoverySourceTrustKeyLifecycleResult.NotFound;
            if (current.Status != AuthorizationRecoverySourceTrustKeyStatus.Active)
                return AuthorizationRecoverySourceTrustKeyLifecycleResult.StaleRotation;
            if (keys.TryGetValue(newKeyId, out var existing))
            {
                if (existing.Status == AuthorizationRecoverySourceTrustKeyStatus.Revoked) return AuthorizationRecoverySourceTrustKeyLifecycleResult.CannotActivateRevokedKey;
                if (existing.Status == AuthorizationRecoverySourceTrustKeyStatus.Active) return AuthorizationRecoverySourceTrustKeyLifecycleResult.AlreadyActive;
                if (existing.Version != newVersion) return AuthorizationRecoverySourceTrustKeyLifecycleResult.ConflictingRotation;
            }
            keys[expectedActiveKeyId] = current with { Status = AuthorizationRecoverySourceTrustKeyStatus.VerificationOnly };
            keys[newKeyId] = new(newKeyId, AuthorizationRecoverySourceTrustKeyStatus.Active, newVersion, newKeyMaterial.ToArray());
            return AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated;
        }
    }

    public AuthorizationRecoverySourceTrustKeyLifecycleResult RevokeTrustedSourceKey(string sourceInstanceId, string keyId)
    {
        lock (_gate)
        {
            if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys) || !keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoverySourceTrustKeyLifecycleResult.NotFound;
            if (key.Status == AuthorizationRecoverySourceTrustKeyStatus.Revoked) return AuthorizationRecoverySourceTrustKeyLifecycleResult.AlreadyRevoked;
            if (key.Status == AuthorizationRecoverySourceTrustKeyStatus.Active) return AuthorizationRecoverySourceTrustKeyLifecycleResult.CannotRevokeActiveKey;
            keys[keyId] = key with { Status = AuthorizationRecoverySourceTrustKeyStatus.Revoked };
            return AuthorizationRecoverySourceTrustKeyLifecycleResult.Revoked;
        }
    }

    public AuthorizationRecoverySourceTrustKeyLifecycleResult RotateLocalSourceKey(
        string expectedActiveKeyId, string newKeyId, int newVersion, ReadOnlySpan<byte> newKeyMaterial)
    {
        if (newVersion < 1) throw new ArgumentOutOfRangeException(nameof(newVersion));
        if (newKeyMaterial.Length < 16) throw new ArgumentException("Replication key must be at least 128 bits.", nameof(newKeyMaterial));
        lock (_gate)
        {
            if (!string.Equals(_localSourceKeyId, expectedActiveKeyId, StringComparison.Ordinal))
                return AuthorizationRecoverySourceTrustKeyLifecycleResult.StaleRotation;
            if (_trustedSourceKeys[_instanceId].ContainsKey(newKeyId))
                return AuthorizationRecoverySourceTrustKeyLifecycleResult.AlreadyActive;
            _trustedSourceKeys[_instanceId][_localSourceKeyId] = new(_localSourceKeyId, AuthorizationRecoverySourceTrustKeyStatus.VerificationOnly, _localSourceKeyVersion, _replicationKey.ToArray());
            _localSourceKeyId = newKeyId;
            _localSourceKeyVersion = newVersion;
            _trustedSourceKeys[_instanceId][newKeyId] = new(newKeyId, AuthorizationRecoverySourceTrustKeyStatus.Active, newVersion, newKeyMaterial.ToArray());
            return AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated;
        }
    }

    public long AuthorityEpoch
    {
        get { lock (_gate) return _authorityEpoch; }
    }

    public AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope CreateEnvelope(
        AuthorizationRecoveryRepairProposerCredentialDurableLifecycle state,
        string? previousDigest = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            var previousSequence = checked(state.CredentialSequence - 1);
            var chainDigest = previousDigest ?? string.Empty;
            var digest = ComputeStateDigest(state, _authorityEpoch);
            var envelope = new AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope(
                state.ProposerId, state.CredentialId, state.CredentialFingerprint,
                state.CredentialSequence, state.State, _authorityEpoch, _instanceId, _localSourceKeyId,
                previousSequence, chainDigest, digest, string.Empty);
            var key = _trustedSourceKeys[_instanceId][_localSourceKeyId];
            return envelope with { IntegrityProof = ComputeIntegrityProof(envelope, key.KeyMaterial) };
        }
    }

    public AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult Apply(
        AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (_gate)
        {
            if (!_trustedSourceKeys.TryGetValue(envelope.SourceInstanceId, out var sourceKeys))
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.UntrustedSource;
            if (!sourceKeys.TryGetValue(envelope.SourceKeyId, out var sourceKey))
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.UnknownSourceKey;
            if (sourceKey.Status == AuthorizationRecoverySourceTrustKeyStatus.Revoked)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.RevokedSourceKey;

            if (!FixedEquals(envelope.IntegrityProof, ComputeIntegrityProof(envelope, sourceKey.KeyMaterial)))
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.InvalidIntegrity;

            if (envelope.AuthorityEpoch != _authorityEpoch)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.AuthorityEpochMismatch;

            if (envelope.SourceInstanceId == _instanceId)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState;

            var current = _states.TryGetValue(envelope.ProposerId, out var existing) ? existing : null;
            var currentSequence = current?.CredentialSequence ?? 0;

            if (envelope.CredentialSequence < currentSequence)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.SequenceRollback;
            if (envelope.CredentialSequence == currentSequence)
            {
                return current is not null && FixedEquals(current.StateDigest, envelope.StateDigest)
                    ? AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Duplicate
                    : AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState;
            }
            if (envelope.CredentialSequence != currentSequence + 1)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.SequenceGap;

            var expectedPreviousDigest = current?.StateDigest ?? string.Empty;
            if (!FixedEquals(expectedPreviousDigest, envelope.PreviousDigest) ||
                envelope.PreviousSequence != currentSequence)
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.PreviousDigestMismatch;

            var reconstructed = new AuthorizationRecoveryRepairProposerCredentialDurableLifecycle(
                envelope.ProposerId, envelope.CredentialId, envelope.CredentialFingerprint,
                envelope.CredentialSequence, envelope.State);
            if (!FixedEquals(envelope.StateDigest, ComputeStateDigest(reconstructed, _authorityEpoch)))
                return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState;

            _states[envelope.ProposerId] = envelope;
            return AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied;
        }
    }

    public void PromoteAuthority(long nextAuthorityEpoch)
    {
        lock (_gate)
        {
            if (nextAuthorityEpoch <= _authorityEpoch)
                throw new InvalidOperationException("Authority epoch must increase monotonically.");
            _authorityEpoch = nextAuthorityEpoch;
            _states.Clear();
        }
    }

    public AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope Snapshot(string proposerId)
    {
        lock (_gate)
            return _states.TryGetValue(proposerId, out var state)
                ? state
                : throw new KeyNotFoundException(proposerId);
    }

    public static string ComputeStateDigest(
        AuthorizationRecoveryRepairProposerCredentialDurableLifecycle state,
        long authorityEpoch)
    {
        var canonical = string.Join("|", authorityEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            state.ProposerId, state.CredentialId, state.CredentialFingerprint,
            state.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            state.State.ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ComputeIntegrityProof(AuthorizationRecoveryRepairProposerCredentialReplicationEnvelope envelope, byte[] replicationKey)
    {
        var canonical = string.Join("|", envelope.ProposerId, envelope.CredentialId,
            envelope.CredentialFingerprint,
            envelope.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            envelope.State.ToString(), envelope.AuthorityEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            envelope.SourceInstanceId, envelope.SourceKeyId, envelope.PreviousSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            envelope.PreviousDigest, envelope.StateDigest);
        using var hmac = new HMACSHA256(replicationKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
