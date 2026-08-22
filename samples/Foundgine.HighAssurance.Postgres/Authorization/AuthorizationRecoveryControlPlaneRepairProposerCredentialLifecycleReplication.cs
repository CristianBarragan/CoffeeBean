using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>
/// M5.71 authoritative lifecycle replication boundary for repair proposer
/// credentials. Lifecycle state is shared across control-plane instances via
/// <see cref="IAuthorizationRecoveryRepairProposerCredentialLifecycleStore"/>;
/// proof key material lives in the distinct
/// <see cref="IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore"/>
/// boundary, so an instance that never locally learned a key can still fetch
/// it from that boundary on demand rather than being permanently unable to
/// authorize a proposer it didn't personally register.
/// </summary>
public sealed record AuthorizationRecoveryRepairProposerCredentialDurableLifecycle(
    string ProposerId,
    string CredentialId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryRepairProposerCredentialState State);

public interface IAuthorizationRecoveryRepairProposerCredentialLifecycleStore
{
    ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle?> ReadAsync(
        string proposerId, CancellationToken cancellationToken = default);

    ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> CompareAndSetAsync(
        AuthorizationRecoveryRepairProposerCredentialDurableLifecycle next,
        long expectedPreviousSequence,
        CancellationToken cancellationToken = default);

    ValueTask<IAuthorizationRecoveryRepairProposerCredentialLifecycleLease?> TryAcquireAsync(
        string proposerId,
        long credentialSequence,
        string credentialFingerprint,
        CancellationToken cancellationToken = default);
}

public interface IAuthorizationRecoveryRepairProposerCredentialLifecycleLease : IAsyncDisposable
{
    AuthorizationRecoveryRepairProposerCredentialDurableLifecycle Snapshot { get; }
    ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A distinct key-management boundary for repair proposer proof keys, kept
/// separate from <see cref="IAuthorizationRecoveryRepairProposerCredentialLifecycleStore"/>
/// so that key material and replicated lifecycle state remain independently
/// accessible/auditable/revocable concepts, even when (as in this in-memory
/// reference implementation) they happen to be hosted by the same process.
/// A production deployment should back this with an actual KMS boundary with
/// its own access policy, separate from the lifecycle control-plane store.
/// Instances publish a key for a proposer's credential *generation* (keyed by
/// <c>CredentialId</c>, not by lifecycle sequence -- a revoke/retire bumps the
/// sequence without minting a new credential, so the same key must still
/// resolve) once they have learned it (e.g. via <c>RegisterAsync</c>), and any
/// instance can subsequently fetch that generation's key to authorize locally.
/// </summary>
public interface IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore
{
    ValueTask<byte[]?> TryGetKeyAsync(
        string proposerId, string credentialId, CancellationToken cancellationToken = default);

    ValueTask PublishKeyAsync(
        string proposerId, string credentialId, ReadOnlyMemory<byte> proofKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reference implementation of the authoritative lifecycle store. The lock is
/// the linearization point for replication, rotation, revocation and lease
/// acquisition. Production deployments must replace this with a durable,
/// linearizable control-plane store.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore
    : IAuthorizationRecoveryRepairProposerCredentialLifecycleStore,
      IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ProposerId, string CredentialId), byte[]> _keyMaterial = new();

    public ValueTask<byte[]?> TryGetKeyAsync(
        string proposerId, string credentialId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return ValueTask.FromResult(
                _keyMaterial.TryGetValue((proposerId, credentialId), out var key) ? key : null);
    }

    public ValueTask PublishKeyAsync(
        string proposerId, string credentialId, ReadOnlyMemory<byte> proofKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _keyMaterial[(proposerId, credentialId)] = proofKey.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle?> ReadAsync(
        string proposerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return ValueTask.FromResult(
                _states.TryGetValue(proposerId, out var state) ? state : null);
    }

    public ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> CompareAndSetAsync(
        AuthorizationRecoveryRepairProposerCredentialDurableLifecycle next,
        long expectedPreviousSequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);
        lock (_gate)
        {
            var actual = _states.TryGetValue(next.ProposerId, out var current)
                ? current.CredentialSequence
                : 0;

            if (actual != expectedPreviousSequence)
                throw new AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
                    next.ProposerId, expectedPreviousSequence, actual);

            if (next.CredentialSequence != checked(actual + 1))
                throw new AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
                    next.ProposerId, actual + 1, next.CredentialSequence);

            _states[next.ProposerId] = next;
            return ValueTask.FromResult(next);
        }
    }

    public ValueTask<IAuthorizationRecoveryRepairProposerCredentialLifecycleLease?> TryAcquireAsync(
        string proposerId,
        long credentialSequence,
        string credentialFingerprint,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(proposerId, out var current) ||
                current.State != AuthorizationRecoveryRepairProposerCredentialState.Active ||
                current.CredentialSequence != credentialSequence ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(current.CredentialFingerprint),
                    Encoding.UTF8.GetBytes(credentialFingerprint)))
                return ValueTask.FromResult<IAuthorizationRecoveryRepairProposerCredentialLifecycleLease?>(null);

            return ValueTask.FromResult<IAuthorizationRecoveryRepairProposerCredentialLifecycleLease?>(
                new Lease(this, current));
        }
    }

    private ValueTask<bool> ValidateAsync(
        AuthorizationRecoveryRepairProposerCredentialDurableLifecycle snapshot,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(snapshot.ProposerId, out var current))
                return ValueTask.FromResult(false);

            var valid = current.State == AuthorizationRecoveryRepairProposerCredentialState.Active &&
                        current.CredentialSequence == snapshot.CredentialSequence &&
                        CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(current.CredentialId),
                            Encoding.UTF8.GetBytes(snapshot.CredentialId)) &&
                        CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(current.CredentialFingerprint),
                            Encoding.UTF8.GetBytes(snapshot.CredentialFingerprint));
            return ValueTask.FromResult(valid);
        }
    }

    private sealed class Lease : IAuthorizationRecoveryRepairProposerCredentialLifecycleLease
    {
        private readonly InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore _owner;
        private int _disposed;

        public Lease(
            InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore owner,
            AuthorizationRecoveryRepairProposerCredentialDurableLifecycle snapshot)
        {
            _owner = owner;
            Snapshot = snapshot;
        }

        public AuthorizationRecoveryRepairProposerCredentialDurableLifecycle Snapshot { get; }

        public ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default) =>
            Volatile.Read(ref _disposed) == 0
                ? _owner.ValidateAsync(Snapshot, cancellationToken)
                : ValueTask.FromResult(false);

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _disposed, 1);
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException : InvalidOperationException
{
    public AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
        string proposerId, long expectedSequence, long actualSequence)
        : base($"Proposer '{proposerId}' lifecycle changed concurrently: expected sequence {expectedSequence}, actual sequence {actualSequence}.")
    {
        ProposerId = proposerId;
        ExpectedSequence = expectedSequence;
        ActualSequence = actualSequence;
    }

    public string ProposerId { get; }
    public long ExpectedSequence { get; }
    public long ActualSequence { get; }
}

/// <summary>
/// Per-instance repair proposer lifecycle replica. It never treats local state
/// as authoritative: authorization acquires an authoritative lifecycle lease,
/// verifies the local proof, then exposes the lease for the existing durable
/// repair commit to perform its final lifecycle gate.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication
{
    private sealed record LocalCredential(
        string CredentialId,
        string Fingerprint,
        long Sequence,
        AuthorizationRecoveryRepairProposerCredentialState State,
        byte[] ProofKey);

    private readonly object _gate = new();
    private readonly Dictionary<string, LocalCredential> _local = new(StringComparer.Ordinal);
    private readonly IAuthorizationRecoveryRepairProposerCredentialLifecycleStore _store;
    private readonly IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore _keyMaterialStore;

    public AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication(
        IAuthorizationRecoveryRepairProposerCredentialLifecycleStore store)
        : this(store, store as IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore
            ?? throw new ArgumentException(
                $"Store must also implement {nameof(IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore)}.",
                nameof(store)))
    {
    }

    public AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication(
        IAuthorizationRecoveryRepairProposerCredentialLifecycleStore store,
        IAuthorizationRecoveryRepairProposerCredentialKeyMaterialStore keyMaterialStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _keyMaterialStore = keyMaterialStore ?? throw new ArgumentNullException(nameof(keyMaterialStore));
    }

    public async ValueTask RegisterAsync(
        string proposerId,
        string credentialId,
        string fingerprint,
        ReadOnlyMemory<byte> proofKey,
        long sequence = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));

        var current = await _store.ReadAsync(proposerId, cancellationToken);
        if (current is null)
        {
            var durable = new AuthorizationRecoveryRepairProposerCredentialDurableLifecycle(
                proposerId, credentialId, fingerprint, sequence,
                AuthorizationRecoveryRepairProposerCredentialState.Active);
            try
            {
                await _store.CompareAndSetAsync(durable, sequence - 1, cancellationToken);
                current = durable;
            }
            catch (AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException)
            {
                current = await _store.ReadAsync(proposerId, cancellationToken);
                if (current is null) throw;
            }
        }

        lock (_gate)
        {
            _local[proposerId] = new(
                current.CredentialId, current.CredentialFingerprint,
                current.CredentialSequence, current.State, proofKey.ToArray());
        }

        // Publish to the shared key-material boundary so any other instance
        // that has not itself been handed this generation's key out-of-band
        // can still fetch it on demand (see RefreshAsync).
        await _keyMaterialStore.PublishKeyAsync(proposerId, current.CredentialId, proofKey, cancellationToken);
    }

    public async ValueTask RefreshAsync(string proposerId, CancellationToken cancellationToken = default)
    {
        var durable = await _store.ReadAsync(proposerId, cancellationToken)
            ?? throw new KeyNotFoundException(proposerId);

        byte[]? key;
        lock (_gate)
        {
            if (_local.TryGetValue(proposerId, out var current))
            {
                if (durable.CredentialSequence < current.Sequence)
                    throw new AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
                        proposerId, current.Sequence, durable.CredentialSequence);

                if (durable.CredentialSequence == current.Sequence &&
                    !string.Equals(durable.CredentialId, current.CredentialId, StringComparison.Ordinal))
                    throw new AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
                        proposerId, current.Sequence, durable.CredentialSequence);

                // A key cached for a different credential generation is
                // useless for the current one; force a re-fetch from the
                // key-material store. A revoke/retire only bumps the
                // sequence and leaves CredentialId unchanged, so this still
                // reuses the cached key across a pure state transition.
                key = string.Equals(current.CredentialId, durable.CredentialId, StringComparison.Ordinal)
                    ? current.ProofKey
                    : null;
            }
            else
            {
                key = null;
            }
        }

        // Never seen this proposer's current generation locally: fall back to
        // the shared key-material boundary and hydrate a local cache entry,
        // mirroring the cross-instance visibility fix used elsewhere in this
        // control plane. Fail closed if no one has ever published a key for
        // this exact credential.
        key ??= await _keyMaterialStore.TryGetKeyAsync(proposerId, durable.CredentialId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No proof key is known for proposer '{proposerId}' credential '{durable.CredentialId}'.");

        lock (_gate)
        {
            _local[proposerId] = new(
                durable.CredentialId, durable.CredentialFingerprint,
                durable.CredentialSequence, durable.State, key);
        }
    }

    public async ValueTask<IAuthorizationRecoveryRepairProposerCredentialLifecycleLease?> TryAuthorizeAsync(
        AuthorizationRecoveryRepairProposerCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (credential is null) return null;

        // Pull authoritative lifecycle state before authentication. A stale
        // replica therefore cannot authorize a revoked or rotated generation.
        await RefreshAsync(credential.ProposerId, cancellationToken);

        LocalCredential local;
        lock (_gate)
        {
            if (!_local.TryGetValue(credential.ProposerId, out local!))
                return null;

            if (local.State != AuthorizationRecoveryRepairProposerCredentialState.Active ||
                local.Sequence != credential.CredentialSequence ||
                !FixedEquals(local.CredentialId, credential.CredentialId) ||
                !FixedEquals(local.Fingerprint, credential.CredentialFingerprint))
                return null;

            if (credential.TargetRevision != credential.ExpectedRevision + 1)
                return null;

            var supplied = TryDecode(credential.Proof);
            if (supplied is null)
                return null;

            using var hmac = new HMACSHA256(local.ProofKey);
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential)));
            if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
                return null;
        }

        // Acquire only after local authentication; the shared store is the
        // authoritative linearization point for the generation.
        return await _store.TryAcquireAsync(
            credential.ProposerId,
            credential.CredentialSequence,
            credential.CredentialFingerprint,
            cancellationToken);
    }

    public async ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> RotateAsync(
        string proposerId,
        string credentialId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await _store.ReadAsync(proposerId, cancellationToken)
            ?? throw new KeyNotFoundException(proposerId);
        if (current.State is AuthorizationRecoveryRepairProposerCredentialState.Revoked
            or AuthorizationRecoveryRepairProposerCredentialState.Retired)
            throw new InvalidOperationException("Revoked or retired proposer credentials cannot be reactivated.");

        var next = current with
        {
            CredentialId = credentialId,
            CredentialFingerprint = fingerprint,
            CredentialSequence = checked(current.CredentialSequence + 1),
            State = AuthorizationRecoveryRepairProposerCredentialState.Active
        };
        var committed = await _store.CompareAndSetAsync(next, current.CredentialSequence, cancellationToken);

        lock (_gate)
        {
            if (_local.TryGetValue(proposerId, out var local))
                _local[proposerId] = local with
                {
                    CredentialId = credentialId,
                    Fingerprint = fingerprint,
                    Sequence = committed.CredentialSequence,
                    State = committed.State
                };
        }
        return committed;
    }

    public async ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> RevokeAsync(
        string proposerId, CancellationToken cancellationToken = default) =>
        await TransitionAsync(proposerId, AuthorizationRecoveryRepairProposerCredentialState.Revoked, cancellationToken);

    public async ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> RetireAsync(
        string proposerId, CancellationToken cancellationToken = default) =>
        await TransitionAsync(proposerId, AuthorizationRecoveryRepairProposerCredentialState.Retired, cancellationToken);

    public async ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> SnapshotAsync(
        string proposerId, CancellationToken cancellationToken = default) =>
        await _store.ReadAsync(proposerId, cancellationToken)
        ?? throw new KeyNotFoundException(proposerId);

    private async ValueTask<AuthorizationRecoveryRepairProposerCredentialDurableLifecycle> TransitionAsync(
        string proposerId,
        AuthorizationRecoveryRepairProposerCredentialState state,
        CancellationToken cancellationToken)
    {
        var current = await _store.ReadAsync(proposerId, cancellationToken)
            ?? throw new KeyNotFoundException(proposerId);
        if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Retired ||
            (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked &&
             state != AuthorizationRecoveryRepairProposerCredentialState.Retired))
            throw new InvalidOperationException("A retired or revoked proposer credential cannot be reactivated.");

        var next = current with
        {
            CredentialSequence = checked(current.CredentialSequence + 1),
            State = state
        };
        var committed = await _store.CompareAndSetAsync(next, current.CredentialSequence, cancellationToken);
        lock (_gate)
        {
            if (_local.TryGetValue(proposerId, out var local))
                _local[proposerId] = local with { Sequence = committed.CredentialSequence, State = state };
        }
        return committed;
    }

    private static byte[]? TryDecode(string proof)
    {
        try { return Convert.FromHexString(proof); }
        catch (FormatException) { return null; }
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static string Canonicalize(AuthorizationRecoveryRepairProposerCredential c)
    {
        static string F(string value) => $"{Encoding.UTF8.GetByteCount(value)}:{value}";
        return string.Concat(
            F("REPAIR-PROPOSER-LIFECYCLE-v1"), F(c.ProposerId), F(c.CredentialId),
            F(c.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.CredentialFingerprint), F(c.TransactionId),
            F(c.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.ExpectedStateFingerprint), F(c.ExpectedJournalHead ?? string.Empty),
            F(c.TargetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.TargetStateFingerprint), F(c.TargetJournalHead ?? string.Empty), F(c.CredentialVersion));
    }
}
