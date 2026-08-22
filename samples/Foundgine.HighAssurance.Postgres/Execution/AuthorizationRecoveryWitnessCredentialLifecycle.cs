using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>Lifecycle state of a witness credential. VerificationOnly permits an overlap window but never becomes the active credential again.</summary>
public enum AuthorizationRecoveryWitnessCredentialState
{
    Active,
    VerificationOnly,
    Revoked,
    Retired
}

/// <summary>Durable-safe witness credential metadata. Secret material is deliberately absent.</summary>
public sealed record AuthorizationRecoveryWitnessCredentialLifecycleSnapshot(
    string WitnessId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryWitnessCredentialState State);

public interface IAuthorizationRecoveryWitnessCredentialLifecycle
{
    void Register(string witnessId, string credentialFingerprint, long credentialSequence = 1);
    bool TryRotate(string witnessId, string newCredentialFingerprint, long expectedSequence, out long newSequence);
    bool TryRevoke(string witnessId, long expectedSequence);
    AuthorizationRecoveryWitnessCredentialLifecycleSnapshot GetSnapshot(string witnessId);
    ValueTask<IAuthorizationRecoveryWitnessCredentialLease?> TryAcquireAsync(
        AuthorizationRecoveryWitnessCredential credential,
        bool allowVerificationOnly = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lease held from witness authentication through a sensitive quorum operation. Rotation may leave
/// the old credential VerificationOnly so in-flight work can finish, while revocation immediately
/// invalidates the lease. A lease never contains secret material.
/// </summary>
public interface IAuthorizationRecoveryWitnessCredentialLease : IAsyncDisposable
{
    AuthorizationRecoveryWitnessCredentialLifecycleSnapshot Snapshot { get; }
    ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthorizationRecoveryWitnessCredentialLifecycle : IAuthorizationRecoveryWitnessCredentialLifecycle
{
    private sealed class Entry
    {
        public required string Fingerprint;
        public long Sequence;
        public AuthorizationRecoveryWitnessCredentialState State;
        public readonly SemaphoreSlim Gate = new(1, 1);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Register(string witnessId, string credentialFingerprint, long credentialSequence = 1)
    {
        Validate(witnessId, credentialFingerprint, credentialSequence);
        lock (_gate)
        {
            if (_entries.ContainsKey(witnessId))
                throw new InvalidOperationException($"Witness '{witnessId}' is already registered.");
            _entries.Add(witnessId, new Entry { Fingerprint = credentialFingerprint, Sequence = credentialSequence, State = AuthorizationRecoveryWitnessCredentialState.Active });
        }
    }

    public bool TryRotate(string witnessId, string newCredentialFingerprint, long expectedSequence, out long newSequence)
    {
        Validate(witnessId, newCredentialFingerprint, expectedSequence);
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(witnessId, out entry!)) { newSequence = 0; return false; }
        }
        entry.Gate.Wait();
        try
        {
            lock (_gate)
            {
                if (entry.Sequence != expectedSequence || entry.State == AuthorizationRecoveryWitnessCredentialState.Revoked || entry.State == AuthorizationRecoveryWitnessCredentialState.Retired)
                { newSequence = entry.Sequence; return false; }
                entry.Fingerprint = newCredentialFingerprint;
                entry.Sequence++;
                entry.State = AuthorizationRecoveryWitnessCredentialState.Active;
                newSequence = entry.Sequence;
                return true;
            }
        }
        finally { entry.Gate.Release(); }
    }

    public bool TryRevoke(string witnessId, long expectedSequence)
    {
        if (string.IsNullOrWhiteSpace(witnessId) || expectedSequence <= 0) return false;
        lock (_gate)
        {
            if (!_entries.TryGetValue(witnessId, out var entry)) return false;
            entry.Gate.Wait();
            try
            {
                if (entry.Sequence != expectedSequence || entry.State == AuthorizationRecoveryWitnessCredentialState.Revoked || entry.State == AuthorizationRecoveryWitnessCredentialState.Retired)
                    return false;
                entry.Sequence++;
                entry.State = AuthorizationRecoveryWitnessCredentialState.Revoked;
                return true;
            }
            finally { entry.Gate.Release(); }
        }
    }

    public AuthorizationRecoveryWitnessCredentialLifecycleSnapshot GetSnapshot(string witnessId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(witnessId, out var entry)) throw new KeyNotFoundException($"Unknown witness '{witnessId}'.");
            return new(witnessId, entry.Fingerprint, entry.Sequence, entry.State);
        }
    }

    public async ValueTask<IAuthorizationRecoveryWitnessCredentialLease?> TryAcquireAsync(
        AuthorizationRecoveryWitnessCredential credential,
        bool allowVerificationOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.WitnessId) || string.IsNullOrWhiteSpace(credential.CredentialFingerprint)) return null;
        Entry entry;
        lock (_gate) { if (!_entries.TryGetValue(credential.WitnessId, out entry!)) return null; }
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            AuthorizationRecoveryWitnessCredentialLifecycleSnapshot snapshot;
            lock (_gate)
            {
                var allowed = entry.State == AuthorizationRecoveryWitnessCredentialState.Active ||
                              (allowVerificationOnly && entry.State == AuthorizationRecoveryWitnessCredentialState.VerificationOnly);
                if (!allowed || !FixedEquals(entry.Fingerprint, credential.CredentialFingerprint)) return null;
                snapshot = new(credential.WitnessId, entry.Fingerprint, entry.Sequence, entry.State);
            }
            return new Lease(this, entry, snapshot);
        }
        finally { entry.Gate.Release(); }
    }

    private sealed class Lease : IAuthorizationRecoveryWitnessCredentialLease
    {
        private readonly AuthorizationRecoveryWitnessCredentialLifecycle _owner;
        private readonly Entry _entry;
        private int _disposed;
        public Lease(AuthorizationRecoveryWitnessCredentialLifecycle owner, Entry entry, AuthorizationRecoveryWitnessCredentialLifecycleSnapshot snapshot) { _owner = owner; _entry = entry; Snapshot = snapshot; }
        public AuthorizationRecoveryWitnessCredentialLifecycleSnapshot Snapshot { get; }
        public ValueTask<bool> ValidateStillCurrentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_owner._gate)
            {
                if (_disposed != 0) return ValueTask.FromResult(false);
                var stateAllowed = _entry.State != AuthorizationRecoveryWitnessCredentialState.Revoked && _entry.State != AuthorizationRecoveryWitnessCredentialState.Retired;
                return ValueTask.FromResult(stateAllowed && _entry.Sequence >= Snapshot.CredentialSequence &&
                    (Snapshot.State == AuthorizationRecoveryWitnessCredentialState.VerificationOnly ||
                     _entry.State == AuthorizationRecoveryWitnessCredentialState.Active ||
                     _entry.State == AuthorizationRecoveryWitnessCredentialState.VerificationOnly));
            }
        }
        public ValueTask DisposeAsync() { Interlocked.Exchange(ref _disposed, 1); return ValueTask.CompletedTask; }
    }

    private static void Validate(string witnessId, string fingerprint, long sequence)
    {
        if (string.IsNullOrWhiteSpace(witnessId)) throw new ArgumentException("Witness ID is required.", nameof(witnessId));
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("Credential fingerprint is required.", nameof(fingerprint));
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
    }

    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}

/// <summary>Lifecycle-backed authenticator: only the current active credential is accepted for normal quorum use.</summary>
public sealed class LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator : IAuthorizationRecoveryWitnessCredentialAuthenticator
{
    private readonly IAuthorizationRecoveryWitnessCredentialLifecycle _lifecycle;
    public LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(IAuthorizationRecoveryWitnessCredentialLifecycle lifecycle) => _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    public bool Authenticate(string witnessId, AuthorizationRecoveryWitnessCredential credential)
    {
        if (string.IsNullOrWhiteSpace(witnessId) || credential is null || !string.Equals(witnessId, credential.WitnessId, StringComparison.Ordinal)) return false;
        try
        {
            var snapshot = _lifecycle.GetSnapshot(witnessId);
            return snapshot.State == AuthorizationRecoveryWitnessCredentialState.Active &&
                   string.Equals(snapshot.CredentialFingerprint, credential.CredentialFingerprint, StringComparison.Ordinal);
        }
        catch (KeyNotFoundException) { return false; }
    }
}
