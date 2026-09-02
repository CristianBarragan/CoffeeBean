namespace Foundgine.Runtime.ControlPlane;

public enum AuthorizationRecoveryAtomicCommitResult
{
 RecoveryPrepared,
 Recovered,
 RecoveryRejectedStaleState,
 RecoveryRejectedRetiredKey,
 RecoveryRejectedIntegrity,
 CommitAbortedBeforeDurableWrite,
 Retired,
 StaleRetirement,
 PublicationCommitted,
 PublicationRejected
}

/// <summary>
/// reference model for the durable transaction boundary introduced by
/// . Revision, key lifecycle, publication, and the accepted recovery
/// outcome are committed as one state transition. A simulated crash before
/// the durable write must leave the previous state completely intact.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneCrossInstanceCommitAtomicity
{
 private readonly object _durableGate = new();
 private readonly Func<string, byte[]?> _keyResolver;
 private readonly long _verificationWindowSequences;
 private AuthorizationRecoveryCrossInstanceState _state;
 private readonly Dictionary<string, long> _recoveredHistoricalSequenceByKey = new(StringComparer.Ordinal);

 public AuthorizationRecoveryControlPlaneCrossInstanceCommitAtomicity(
 AuthorizationRecoveryCrossInstanceState initialState,
 long verificationWindowSequences,
 Func<string, byte[]?> keyResolver)
 {
 ArgumentNullException.ThrowIfNull(initialState);
 ArgumentNullException.ThrowIfNull(keyResolver);
 if (verificationWindowSequences < 0)
 throw new ArgumentOutOfRangeException(nameof(verificationWindowSequences));

 _state = initialState;
 _verificationWindowSequences = verificationWindowSequences;
 _keyResolver = keyResolver;
 }

 public AuthorizationRecoveryCrossInstanceState Current
 {
 get { lock (_durableGate) return _state; }
 }

 public AuthorizationRecoveryAtomicCommitResult TryPrepareHistoricalRecovery(
 AuthorizationRecoveryControlPlanePublication historicalPublication,
 out AuthorizationRecoveryCrossInstanceDecision? decision)
 {
 ArgumentNullException.ThrowIfNull(historicalPublication);

 lock (_durableGate)
 {
 decision = null;

 if (!_state.KeyRing.Keys.TryGetValue(historicalPublication.IntegrityKeyId, out var key) ||
 key.Status == AuthorizationRecoveryKeyStatus.Retired)
 return AuthorizationRecoveryAtomicCommitResult.RecoveryRejectedRetiredKey;

 // The verification window gates retirement of the signing generation
 // (see TryRetire), not eligibility to recover a historical publication.
 var material = _keyResolver(historicalPublication.IntegrityKeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(historicalPublication, material))
 return AuthorizationRecoveryAtomicCommitResult.RecoveryRejectedIntegrity;

 decision = new AuthorizationRecoveryCrossInstanceDecision(
 _state.Revision,
 historicalPublication,
 historicalPublication.IntegrityKeyId);

 return AuthorizationRecoveryAtomicCommitResult.RecoveryPrepared;
 }
 }

 /// <summary>
 /// Atomically commits recovery. All validation happens against the current
 /// durable state, then the complete next state is installed in one write.
 /// If crashBeforeDurableWrite is true, no part of the transition is stored.
 /// </summary>
 public AuthorizationRecoveryAtomicCommitResult TryCommitHistoricalRecovery(
 AuthorizationRecoveryCrossInstanceDecision decision,
 bool crashBeforeDurableWrite = false)
 {
 ArgumentNullException.ThrowIfNull(decision);

 lock (_durableGate)
 {
 if (decision.Revision != _state.Revision)
 return AuthorizationRecoveryAtomicCommitResult.RecoveryRejectedStaleState;

 if (!_state.KeyRing.Keys.TryGetValue(decision.KeyId, out var key) ||
 key.Status == AuthorizationRecoveryKeyStatus.Retired)
 return AuthorizationRecoveryAtomicCommitResult.RecoveryRejectedRetiredKey;

 var material = _keyResolver(decision.KeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(decision.Publication, material))
 return AuthorizationRecoveryAtomicCommitResult.RecoveryRejectedIntegrity;

 if (crashBeforeDurableWrite)
 return AuthorizationRecoveryAtomicCommitResult.CommitAbortedBeforeDurableWrite;

 // Recovery is an atomic durable transition. The reference model has
 // no intermediate observable state between validation and assignment.
 _state = _state with { Revision = checked(_state.Revision + 1) };

 if (!_recoveredHistoricalSequenceByKey.TryGetValue(decision.KeyId, out var trackedSequence) ||
 decision.Publication.Sequence > trackedSequence)
 _recoveredHistoricalSequenceByKey[decision.KeyId] = decision.Publication.Sequence;

 return AuthorizationRecoveryAtomicCommitResult.Recovered;
 }
 }

 /// <summary>
 /// Atomically retires a verification-only key. The lifecycle mutation and
 /// durable revision advance together; an injected pre-write crash changes
 /// neither.
 /// </summary>
 public AuthorizationRecoveryAtomicCommitResult TryRetire(
 string keyId,
 string expectedActiveKeyId,
 long expectedSequence,
 long expectedRevision,
 bool crashBeforeDurableWrite = false)
 {
 lock (_durableGate)
 {
 if (expectedRevision != _state.Revision ||
 !string.Equals(expectedActiveKeyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal) ||
 expectedSequence != _state.Publication.Sequence)
 return AuthorizationRecoveryAtomicCommitResult.StaleRetirement;

 if (!_state.KeyRing.Keys.TryGetValue(keyId, out var key) ||
 string.Equals(keyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal) ||
 key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly)
 return AuthorizationRecoveryAtomicCommitResult.StaleRetirement;

 var protectedFrom = _state.Publication.Sequence - _verificationWindowSequences;
 var currentPublicationProtected =
 string.Equals(_state.Publication.IntegrityKeyId, keyId, StringComparison.Ordinal) &&
 _state.Publication.Sequence >= protectedFrom;
 var recoveredHistoricalProtected =
 _recoveredHistoricalSequenceByKey.TryGetValue(keyId, out var recoveredSequence) &&
 recoveredSequence >= protectedFrom;

 if (currentPublicationProtected || recoveredHistoricalProtected)
 return AuthorizationRecoveryAtomicCommitResult.StaleRetirement;

 if (crashBeforeDurableWrite)
 return AuthorizationRecoveryAtomicCommitResult.CommitAbortedBeforeDurableWrite;

 var nextKeys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(
 _state.KeyRing.Keys, StringComparer.Ordinal)
 {
 [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
 };

 _state = _state with
 {
 Revision = checked(_state.Revision + 1),
 KeyRing = new AuthorizationRecoveryKeyRing(_state.KeyRing.ActiveKeyId, nextKeys)
 };

 return AuthorizationRecoveryAtomicCommitResult.Retired;
 }
 }

 public AuthorizationRecoveryAtomicCommitResult TryPublish(
 string expectedActiveKeyId,
 long expectedRevision,
 AuthorizationRecoveryControlPlanePublication publication,
 bool crashBeforeDurableWrite = false)
 {
 ArgumentNullException.ThrowIfNull(publication);

 lock (_durableGate)
 {
 if (expectedRevision != _state.Revision ||
 !string.Equals(expectedActiveKeyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal) ||
 publication.Sequence < _state.Publication.Sequence ||
 !string.Equals(publication.IntegrityKeyId, expectedActiveKeyId, StringComparison.Ordinal))
 return AuthorizationRecoveryAtomicCommitResult.PublicationRejected;

 if (!_state.KeyRing.Keys.TryGetValue(expectedActiveKeyId, out var key) ||
 key.Status != AuthorizationRecoveryKeyStatus.Active)
 return AuthorizationRecoveryAtomicCommitResult.PublicationRejected;

 var material = _keyResolver(expectedActiveKeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(publication, material))
 return AuthorizationRecoveryAtomicCommitResult.PublicationRejected;

 if (crashBeforeDurableWrite)
 return AuthorizationRecoveryAtomicCommitResult.CommitAbortedBeforeDurableWrite;

 _state = _state with
 {
 Revision = checked(_state.Revision + 1),
 Publication = publication
 };

 return AuthorizationRecoveryAtomicCommitResult.PublicationCommitted;
 }
 }
}
