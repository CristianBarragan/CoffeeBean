namespace Foundgine.Security.Authority;

/// <summary>Lifecycle state of a proposer credential.</summary>
public enum AuthorizationRecoveryReconfigurationProposerCredentialState
{
    Active,
    VerificationOnly,
    Revoked,
    Retired
}

/// <summary>Immutable lifecycle snapshot used to bind an in-flight reconfiguration to one credential generation.</summary>
public sealed record AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot(
    string ProposerId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryReconfigurationProposerCredentialState State);

/// <summary>Lease held from proposer authentication through durable reconfiguration commit.</summary>
public interface IAuthorizationRecoveryReconfigurationProposerCredentialLease : IAsyncDisposable
{
    AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Snapshot { get; }

    /// <summary>Rechecks the authoritative lifecycle store before durable commit.</summary>
    ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional lifecycle boundary for proposer credentials. Implementations must serialize credential
/// rotation/retirement against an acquired reconfiguration lease so a credential cannot change
/// between authorization and durable commit.
/// </summary>
public interface IAuthorizationRecoveryReconfigurationProposerCredentialLifecycle
{
    ValueTask<IAuthorizationRecoveryReconfigurationProposerCredentialLease?> TryAcquireAsync(
        AuthorizationRecoveryReconfigurationProposerCredential credential,
        CancellationToken cancellationToken = default);

    AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot GetSnapshot(string proposerId);
}

/// <summary>
/// Reference/test credential lifecycle manager. Production should place the lifecycle state behind
/// an independent control-plane/KMS/HSM boundary. Secrets are kept outside PostgreSQL.
/// </summary>
public sealed class AuthorizationRecoveryReconfigurationProposerCredentialLifecycle
    : IAuthorizationRecoveryReconfigurationProposerCredentialLifecycle
{
    private sealed class Entry
    {
        public required string Fingerprint;
        public long Sequence;
        public AuthorizationRecoveryReconfigurationProposerCredentialState State;
        public readonly SemaphoreSlim Gate = new(1, 1);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IAuthorizationRecoveryProposerCredentialRevocationStore? _store;
    private readonly AuthorizationRecoveryProposerCredentialAuditLedger? _auditLedger;

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(IAuthorizationRecoveryProposerCredentialRevocationStore? store = null, AuthorizationRecoveryProposerCredentialAuditLedger? auditLedger = null)
    {
        _store = store;
        _auditLedger = auditLedger;
    }

    public void Register(string proposerId, string credentialFingerprint, long credentialSequence = 1)
    {
        if (string.IsNullOrWhiteSpace(proposerId)) throw new ArgumentException("Proposer ID is required.", nameof(proposerId));
        if (string.IsNullOrWhiteSpace(credentialFingerprint)) throw new ArgumentException("Credential fingerprint is required.", nameof(credentialFingerprint));
        if (credentialSequence <= 0) throw new ArgumentOutOfRangeException(nameof(credentialSequence));
        lock (_gate)
        {
            if (_entries.ContainsKey(proposerId)) throw new InvalidOperationException($"Proposer '{proposerId}' is already registered.");
        }

        if (_store is not null)
        {
            var durable = _store.ReadAsync(proposerId).GetAwaiter().GetResult();
            if (durable is not null)
            {
                lock (_gate)
                {
                    _entries.Add(proposerId, new Entry { Fingerprint = durable.CredentialFingerprint, Sequence = durable.CredentialSequence, State = durable.State });
                }
                return;
            }

            _store.WriteAsync(
                new AuthorizationRecoveryProposerCredentialDurableState(proposerId, credentialFingerprint, credentialSequence, AuthorizationRecoveryReconfigurationProposerCredentialState.Active),
                0).GetAwaiter().GetResult();
        }

        lock (_gate)
        {
            _entries.Add(proposerId, new Entry { Fingerprint = credentialFingerprint, Sequence = credentialSequence, State = AuthorizationRecoveryReconfigurationProposerCredentialState.Active });
        }
        _auditLedger?.Append(proposerId, credentialFingerprint, credentialSequence, AuthorizationRecoveryReconfigurationProposerCredentialState.Active);
    }

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Rotate(string proposerId, string newCredentialFingerprint)
    {
        if (string.IsNullOrWhiteSpace(newCredentialFingerprint)) throw new ArgumentException("Credential fingerprint is required.", nameof(newCredentialFingerprint));
        var entry = GetEntry(proposerId);
        entry.Gate.Wait();
        try
        {
            lock (_gate)
            {
                if (entry.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Retired ||
                    entry.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked)
                    throw new InvalidOperationException("A retired or revoked proposer credential cannot be reactivated by rotation.");
                checked { entry.Sequence++; }
                entry.Fingerprint = newCredentialFingerprint;
                entry.State = AuthorizationRecoveryReconfigurationProposerCredentialState.Active;
                var snapshot = Snapshot(proposerId, entry);
                PersistSnapshot(snapshot, previousSequence: snapshot.CredentialSequence - 1);
                _auditLedger?.Append(snapshot.ProposerId, snapshot.CredentialFingerprint, snapshot.CredentialSequence, snapshot.State);
                return snapshot;
            }
        }
        finally { entry.Gate.Release(); }
    }

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot SetVerificationOnly(string proposerId)
        => SetState(proposerId, AuthorizationRecoveryReconfigurationProposerCredentialState.VerificationOnly);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Retire(string proposerId)
        => SetState(proposerId, AuthorizationRecoveryReconfigurationProposerCredentialState.Retired);

    /// <summary>Revokes the current credential generation immediately after any already-held lease releases.</summary>
    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Revoke(string proposerId)
        => SetState(proposerId, AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot GetSnapshot(string proposerId)
    {
        var entry = GetEntry(proposerId);
        lock (_gate) return Snapshot(proposerId, entry);
    }

    public async ValueTask<IAuthorizationRecoveryReconfigurationProposerCredentialLease?> TryAcquireAsync(
        AuthorizationRecoveryReconfigurationProposerCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.ProposerId)) return null;
        Entry? entry;
        lock (_gate) _entries.TryGetValue(credential.ProposerId, out entry);

        if (entry is null)
        {
            if (_store is null) return null;
            var durable = await _store.ReadAsync(credential.ProposerId, cancellationToken);
            if (durable is null) return null;
            lock (_gate)
            {
                if (!_entries.TryGetValue(credential.ProposerId, out entry))
                {
                    entry = new Entry { Fingerprint = durable.CredentialFingerprint, Sequence = durable.CredentialSequence, State = durable.State };
                    _entries.Add(credential.ProposerId, entry);
                }
            }
        }

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            await RefreshFromStoreAsync(credential.ProposerId, entry, cancellationToken);
        }
        catch
        {
            entry.Gate.Release();
            throw;
        }
        lock (_gate)
        {
            if (entry.State != AuthorizationRecoveryReconfigurationProposerCredentialState.Active ||
                entry.Sequence != credential.CredentialSequence ||
                !string.Equals(entry.Fingerprint, credential.CredentialFingerprint, StringComparison.Ordinal))
            {
                entry.Gate.Release();
                return null;
            }
            return new Lease(this, credential.ProposerId, entry, Snapshot(credential.ProposerId, entry));
        }
    }

    private AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot SetState(string proposerId, AuthorizationRecoveryReconfigurationProposerCredentialState state)
    {
        var entry = GetEntry(proposerId);
        entry.Gate.Wait();
        try
        {
            lock (_gate)
            {
                if (entry.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Retired ||
                    (entry.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked && state != AuthorizationRecoveryReconfigurationProposerCredentialState.Retired))
                    throw new InvalidOperationException("A retired or revoked proposer credential cannot be reactivated.");
                checked { entry.Sequence++; }
                entry.State = state;
                var snapshot = Snapshot(proposerId, entry);
                PersistSnapshot(snapshot, previousSequence: snapshot.CredentialSequence - 1);
                _auditLedger?.Append(snapshot.ProposerId, snapshot.CredentialFingerprint, snapshot.CredentialSequence, snapshot.State);
                return snapshot;
            }
        }
        finally { entry.Gate.Release(); }
    }

    private void PersistSnapshot(AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot snapshot, long previousSequence)
    {
        if (_store is null) return;
        _store.WriteAsync(
            new AuthorizationRecoveryProposerCredentialDurableState(snapshot.ProposerId, snapshot.CredentialFingerprint, snapshot.CredentialSequence, snapshot.State),
            previousSequence).GetAwaiter().GetResult();
    }

    private async ValueTask<bool> ValidateLeaseAsync(
        string proposerId,
        Entry entry,
        AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (_store is null) return true;
        var durable = await _store.ReadAsync(proposerId, cancellationToken);
        return durable is not null &&
               durable.CredentialSequence == snapshot.CredentialSequence &&
               durable.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Active &&
               string.Equals(durable.CredentialFingerprint, snapshot.CredentialFingerprint, StringComparison.Ordinal);
    }

    private async ValueTask RefreshFromStoreAsync(string proposerId, Entry entry, CancellationToken cancellationToken)
    {
        if (_store is null) return;
        var durable = await _store.ReadAsync(proposerId, cancellationToken);
        if (durable is null) return;
        lock (_gate)
        {
            if (durable.CredentialSequence < entry.Sequence)
                throw new AuthorizationRecoveryProposerCredentialRevocationConflictException(proposerId, entry.Sequence, durable.CredentialSequence);
            entry.Fingerprint = durable.CredentialFingerprint;
            entry.Sequence = durable.CredentialSequence;
            entry.State = durable.State;
        }
    }

    private Entry GetEntry(string proposerId)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(proposerId, out var cached))
                return cached;
        }

        // Not seen by this instance yet: another instance sharing the same durable
        // store may have registered/rotated/revoked this proposer. Resolve from the
        // store rather than failing closed on a purely local cache miss.
        if (_store is not null)
        {
            var durable = _store.ReadAsync(proposerId).GetAwaiter().GetResult();
            if (durable is not null)
            {
                lock (_gate)
                {
                    if (!_entries.TryGetValue(proposerId, out var entry))
                    {
                        entry = new Entry { Fingerprint = durable.CredentialFingerprint, Sequence = durable.CredentialSequence, State = durable.State };
                        _entries.Add(proposerId, entry);
                    }
                    return entry;
                }
            }
        }

        throw new KeyNotFoundException($"Unknown proposer '{proposerId}'.");
    }

    private static AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Snapshot(string proposerId, Entry entry) =>
        new(proposerId, entry.Fingerprint, entry.Sequence, entry.State);

    private sealed class Lease : IAuthorizationRecoveryReconfigurationProposerCredentialLease
    {
        private readonly AuthorizationRecoveryReconfigurationProposerCredentialLifecycle _owner;
        private readonly string _proposerId;
        private readonly Entry _entry;
        private int _disposed;

        public Lease(AuthorizationRecoveryReconfigurationProposerCredentialLifecycle owner, string proposerId, Entry entry, AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot snapshot)
        {
            _owner = owner; _proposerId = proposerId; _entry = entry; Snapshot = snapshot;
        }

        public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot Snapshot { get; }

        public async ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
            return await _owner.ValidateLeaseAsync(_proposerId, _entry, Snapshot, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _entry.Gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
