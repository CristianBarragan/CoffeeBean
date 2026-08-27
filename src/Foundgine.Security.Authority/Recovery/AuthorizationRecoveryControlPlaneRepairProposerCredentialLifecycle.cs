using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Security.Authority;

// ---------------------------------------------------------------------------
// Repair proposer credential lifecycle, replication, and integrity.
//
// This file consolidates what was previously three separately-numbered
// milestones (, , /73/74) into one control: authenticating and
// authorizing the identity allowed to *propose* an authorization-recovery
// repair, across a single instance, replicated across instances, and with
// source-authenticated integrity on that replication. They were split by
// changelog history, not by architecture, and belong in one file.
//
// Section 1 () — AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle
// Single-instance credential lifecycle fence: register, rotate, revoke,
// retire, and authorize a proposer credential under one lock, so there is
// no gap between a lifecycle transition and authorization acceptance.
//
// Section 2 () — AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication
// The same lifecycle, but backed by a shared/durable store so multiple
// control-plane instances agree on proposer credential state, with a
// distinct key-material boundary for proof keys.
//
// Section 3 (/73/74) — AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity
// Source-authenticated, ordered, tamper-evident replication of that
// lifecycle state between instances (envelope integrity, authority-epoch
// fencing, sequence/digest chaining, and per-source trust-key rotation
// and revocation).
// ---------------------------------------------------------------------------

#region Section 1: Single-instance lifecycle ()

/// <summary>Outcome of a proposer credential lifecycle transition.</summary>
public enum AuthorizationRecoveryRepairProposerCredentialLifecycleResult
{
 Rotated,
 AlreadyCurrent,
 UnknownProposer,
 InvalidTransition,
 Revoked,
 Retired
}

/// <summary>Atomic authorization result for an in-flight repair attempt.</summary>
public enum AuthorizationRecoveryRepairProposerCredentialAttemptResult
{
 Authorized,
 UnknownProposer,
 InvalidCredential,
 CredentialNotActive,
 CredentialSequenceMismatch,
 CredentialFingerprintMismatch,
 TransactionBindingMismatch,
 StateBindingMismatch,
 ProofMismatch
}

/// <summary>
/// Single-instance lifecycle fence for repair proposer credentials. Rotation
/// and revocation linearize against authorization under one control-plane
/// lock. An attempt that acquires the lock before a lifecycle transition may
/// finish under the old credential; an attempt that acquires it after the
/// transition is rejected. There is no check-then-use gap between lifecycle
/// validation and authorization acceptance.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle
{
 private sealed record Registration(
 string CredentialId,
 string Fingerprint,
 long Sequence,
 AuthorizationRecoveryRepairProposerCredentialState State,
 byte[] ProofKey);

 private readonly object _gate = new();
 private readonly Dictionary<string, Registration> _proposers = new(StringComparer.Ordinal);

 public void Register(string proposerId, string credentialId, string fingerprint, ReadOnlySpan<byte> proofKey, long sequence = 1)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
 ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
 ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
 if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
 if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));

 lock (_gate)
 {
 _proposers[proposerId] = new(credentialId, fingerprint, sequence,
 AuthorizationRecoveryRepairProposerCredentialState.Active, proofKey.ToArray());
 }
 }

 public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Rotate(
 string proposerId, string credentialId, string fingerprint, ReadOnlySpan<byte> proofKey)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
 ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
 ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
 if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));

 lock (_gate)
 {
 if (!_proposers.TryGetValue(proposerId, out var current))
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
 if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;
 if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Retired)
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Retired;
 if (string.Equals(current.CredentialId, credentialId, StringComparison.Ordinal) &&
 string.Equals(current.Fingerprint, fingerprint, StringComparison.Ordinal))
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.AlreadyCurrent;

 _proposers[proposerId] = new(credentialId, fingerprint, checked(current.Sequence + 1),
 AuthorizationRecoveryRepairProposerCredentialState.Active, proofKey.ToArray());
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
 }
 }

 public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Revoke(string proposerId)
 {
 lock (_gate)
 {
 if (!_proposers.TryGetValue(proposerId, out var current))
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
 if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;

 _proposers[proposerId] = current with { State = AuthorizationRecoveryRepairProposerCredentialState.Revoked };
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
 }
 }

 public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Retire(string proposerId)
 {
 lock (_gate)
 {
 if (!_proposers.TryGetValue(proposerId, out var current))
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
 if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;
 if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Retired)
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Retired;

 _proposers[proposerId] = current with { State = AuthorizationRecoveryRepairProposerCredentialState.Retired };
 return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
 }
 }

 /// <summary>
 /// Performs credential lifecycle validation and proof verification while
 /// holding the same lock used by Rotate/Revoke/Retire. This is the
 /// linearization point for authorization.
 /// </summary>
 public AuthorizationRecoveryRepairProposerCredentialAttemptResult Authorize(
 AuthorizationRecoveryRepairProposerCredential credential)
 {
 if (credential is null || string.IsNullOrWhiteSpace(credential.ProposerId) ||
 string.IsNullOrWhiteSpace(credential.CredentialId) || credential.CredentialSequence < 1 ||
 string.IsNullOrWhiteSpace(credential.TransactionId) || string.IsNullOrWhiteSpace(credential.ExpectedStateFingerprint) ||
 string.IsNullOrWhiteSpace(credential.TargetStateFingerprint))
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;

 lock (_gate)
 {
 if (!_proposers.TryGetValue(credential.ProposerId, out var current))
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.UnknownProposer;
 if (current.State != AuthorizationRecoveryRepairProposerCredentialState.Active)
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialNotActive;
 if (!string.Equals(credential.CredentialVersion, "v1", StringComparison.Ordinal))
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;
 if (credential.CredentialSequence != current.Sequence)
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialSequenceMismatch;
 if (!FixedEquals(credential.CredentialId, current.CredentialId))
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;
 if (!FixedEquals(credential.CredentialFingerprint, current.Fingerprint))
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialFingerprintMismatch;
 if (credential.TargetRevision != credential.ExpectedRevision + 1)
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.StateBindingMismatch;

 var supplied = TryDecode(credential.Proof);
 if (supplied is null)
 return AuthorizationRecoveryRepairProposerCredentialAttemptResult.ProofMismatch;

 using var hmac = new HMACSHA256(current.ProofKey);
 var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential)));
 return CryptographicOperations.FixedTimeEquals(supplied, expected)
 ? AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized
 : AuthorizationRecoveryRepairProposerCredentialAttemptResult.ProofMismatch;
 }
 }

 public (string CredentialId, string Fingerprint, long Sequence, AuthorizationRecoveryRepairProposerCredentialState State) Snapshot(string proposerId)
 {
 lock (_gate)
 {
 if (!_proposers.TryGetValue(proposerId, out var current)) throw new KeyNotFoundException(proposerId);
 return (current.CredentialId, current.Fingerprint, current.Sequence, current.State);
 }
 }

 public static string CreateProof(AuthorizationRecoveryRepairProposerCredential credential, ReadOnlySpan<byte> proofKey)
 {
 using var hmac = new HMACSHA256(proofKey.ToArray());
 return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential))));
 }

 private static byte[]? TryDecode(string proof)
 {
 try { return Convert.FromHexString(proof); }
 catch (FormatException) { return null; }
 }

 private static bool FixedEquals(string left, string right) =>
 CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

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

#endregion

#region Section 2: Cross-instance replicated lifecycle ()

/// <summary>
/// Authoritative lifecycle replication boundary for repair proposer
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
 long expectedPreviousSequence,
 CancellationToken cancellationToken = default)
 {
 if (expectedPreviousSequence < 0)
 throw new ArgumentOutOfRangeException(nameof(expectedPreviousSequence));

 // Rotation is optimistic concurrency, not a read-and-then-rotate command.
 // The sequence observed by the caller is part of the mutation intent.
 // Without that fence, a losing concurrent caller can reread the winner's
 // new sequence and legitimately perform a second rotation, turning one
 // concurrent generation change into N sequential changes.
 var current = await _store.ReadAsync(proposerId, cancellationToken)
 ?? throw new KeyNotFoundException(proposerId);

 // Terminal-state is checked against the authoritative record before
 // the caller's expected-sequence fence. Revoked/retired is permanent:
 // it must never be masked by a sequence conflict, or a stale replica
 // that raced a revocation would see a retryable conflict instead of
 // the terminal rejection, and could be misled into thinking a retry
 // with the fresh sequence might succeed.
 if (current.State is AuthorizationRecoveryRepairProposerCredentialState.Revoked
 or AuthorizationRecoveryRepairProposerCredentialState.Retired)
 throw new InvalidOperationException("Revoked or retired proposer credentials cannot be reactivated.");
 if (current.CredentialSequence != expectedPreviousSequence)
 throw new AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException(
 proposerId, expectedPreviousSequence, current.CredentialSequence);

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

#endregion

#region Section 3: Source-authenticated replication integrity (/73/74)

/// <summary>
/// Source-authenticated replication envelope for proposer credential lifecycle
/// state. The envelope is ordered by authority epoch, lifecycle sequence and
/// previous digest.
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
/// Integrity, ordering, and source-trust boundary for replicating repair
/// proposer credential lifecycle state between control-plane instances.
/// Replication messages are accepted only when authenticated by a trusted,
/// non-revoked source trust key, belong to the current authority epoch,
/// advance exactly one lifecycle sequence, and chain to the current state
/// digest. Source identity is cryptographically bound to its key, so an
/// envelope cannot simply rewrite <c>SourceInstanceId</c> and remain valid.
/// Per-source trust keys additionally support atomic rotation (old key
/// demoted to verification-only, not immediately invalidated, so in-flight
/// envelopes still verify) and terminal revocation.
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

#endregion