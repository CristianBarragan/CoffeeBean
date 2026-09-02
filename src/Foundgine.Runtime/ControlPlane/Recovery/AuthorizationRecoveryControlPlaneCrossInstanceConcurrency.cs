namespace Foundgine.Runtime.ControlPlane;

/// <summary>
/// Durable cross-instance state version. Every authoritative key-lifecycle or
/// publication transition advances the revision. Recovery must commit against
/// the revision it observed; this turns the recovery decision into a
/// compare-and-swap against shared durable control-plane state.
/// </summary>
public sealed record AuthorizationRecoveryCrossInstanceState(
 long Revision,
 AuthorizationRecoveryKeyRing KeyRing,
 AuthorizationRecoveryControlPlanePublication Publication);

public sealed record AuthorizationRecoveryCrossInstanceDecision(
 long Revision,
 AuthorizationRecoveryControlPlanePublication Publication,
 string KeyId);

public enum AuthorizationRecoveryCrossInstanceResult
{
 RecoveryPrepared,
 Recovered,
 RecoveryRejectedStaleState,
 RecoveryRejectedRetiredKey,
 RecoveryRejectedPublication,
 RecoveryRejectedIntegrity,
 Retired,
 StaleRetirement,
 HistoricalPublicationStillProtected,
 ConcurrentRetirementLost,
 PublicationCommitted,
 PublicationRejected
}

/// <summary>
/// Reference model for the distributed boundary.
///
/// This object represents the shared durable control-plane state, not an
/// in-process instance. Multiple recovery/retirement coordinators hold a
/// reference to the same store and therefore contend through the same
/// durable revision. Production implementations should map Revision to a
/// database row/version, transaction sequence, or equivalent compare-and-swap
/// primitive. A local process lock is intentionally not the security boundary.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneCrossInstanceConcurrency
{
 private readonly object _durableGate = new();
 private readonly Func<string, byte[]?> _keyResolver;
 private readonly long _verificationWindowSequences;
 private AuthorizationRecoveryCrossInstanceState _state;
 private readonly Dictionary<string, long> _recoveredHistoricalSequenceByKey = new(StringComparer.Ordinal);

 public AuthorizationRecoveryControlPlaneCrossInstanceConcurrency(
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

 /// <summary>
 /// Reads shared durable state and prepares a recovery decision. Preparation
 /// is deliberately not sufficient to authorize recovery: the decision
 /// carries the observed revision and must be committed with CAS.
 /// </summary>
 public AuthorizationRecoveryCrossInstanceResult TryPrepareHistoricalRecovery(
 AuthorizationRecoveryControlPlanePublication historicalPublication,
 out AuthorizationRecoveryCrossInstanceDecision? decision)
 {
 ArgumentNullException.ThrowIfNull(historicalPublication);

 lock (_durableGate)
 {
 decision = null;

 if (!_state.KeyRing.Keys.TryGetValue(
 historicalPublication.IntegrityKeyId, out var key) ||
 key.Status == AuthorizationRecoveryKeyStatus.Retired)
 {
 return AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedRetiredKey;
 }

 // The verification window gates retirement of the signing generation
 // (see TryRetire), not eligibility to recover a historical publication.
 if (_keyResolver(historicalPublication.IntegrityKeyId) is not { } material ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
 historicalPublication, material))
 {
 return AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedIntegrity;
 }

 decision = new AuthorizationRecoveryCrossInstanceDecision(
 _state.Revision,
 historicalPublication,
 historicalPublication.IntegrityKeyId);

 return AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared;
 }
 }

 /// <summary>
 /// Commits a prepared recovery against the shared durable revision.
 /// Retirement, publication, and another conflicting transition all advance
 /// the revision. Therefore a decision prepared by instance A cannot remain
 /// valid after instance B commits a conflicting durable transition.
 /// </summary>
 public AuthorizationRecoveryCrossInstanceResult TryCommitHistoricalRecovery(
 AuthorizationRecoveryCrossInstanceDecision decision)
 {
 ArgumentNullException.ThrowIfNull(decision);

 lock (_durableGate)
 {
 if (decision.Revision != _state.Revision)
 return AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedStaleState;

 if (!_state.KeyRing.Keys.TryGetValue(decision.KeyId, out var key) ||
 key.Status == AuthorizationRecoveryKeyStatus.Retired)
 {
 return AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedRetiredKey;
 }

 var material = _keyResolver(decision.KeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
 decision.Publication, material))
 {
 return AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedIntegrity;
 }

 if (!_recoveredHistoricalSequenceByKey.TryGetValue(decision.KeyId, out var trackedSequence) ||
 decision.Publication.Sequence > trackedSequence)
 _recoveredHistoricalSequenceByKey[decision.KeyId] = decision.Publication.Sequence;

 return AuthorizationRecoveryCrossInstanceResult.Recovered;
 }
 }

 /// <summary>
 /// Retires a verification-only generation using an optimistic durable
 /// revision check. A stale instance cannot retire against newer state.
 /// </summary>
 public AuthorizationRecoveryCrossInstanceResult TryRetire(
 string keyId,
 string expectedActiveKeyId,
 long expectedSequence,
 long expectedRevision)
 {
 lock (_durableGate)
 {
 if (expectedRevision != _state.Revision ||
 !string.Equals(
 expectedActiveKeyId,
 _state.KeyRing.ActiveKeyId,
 StringComparison.Ordinal) ||
 expectedSequence != _state.Publication.Sequence)
 {
 return AuthorizationRecoveryCrossInstanceResult.StaleRetirement;
 }

 if (!_state.KeyRing.Keys.TryGetValue(keyId, out var key))
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;

 if (key.Status == AuthorizationRecoveryKeyStatus.Retired)
 return AuthorizationRecoveryCrossInstanceResult.ConcurrentRetirementLost;

 if (string.Equals(keyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal) ||
 key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly)
 {
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;
 }

 var protectedFrom =
 _state.Publication.Sequence - _verificationWindowSequences;

 var currentPublicationProtected = string.Equals(
 _state.Publication.IntegrityKeyId,
 keyId,
 StringComparison.Ordinal) &&
 _state.Publication.Sequence >= protectedFrom;
 var recoveredHistoricalProtected =
 _recoveredHistoricalSequenceByKey.TryGetValue(keyId, out var recoveredSequence) &&
 recoveredSequence >= protectedFrom;

 if (currentPublicationProtected || recoveredHistoricalProtected)
 {
 return AuthorizationRecoveryCrossInstanceResult.HistoricalPublicationStillProtected;
 }

 var nextKeys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(
 _state.KeyRing.Keys, StringComparer.Ordinal)
 {
 [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
 };

 _state = _state with
 {
 Revision = checked(_state.Revision + 1),
 KeyRing = new AuthorizationRecoveryKeyRing(
 _state.KeyRing.ActiveKeyId, nextKeys)
 };

 return AuthorizationRecoveryCrossInstanceResult.Retired;
 }
 }

 /// <summary>
 /// Publishes new authoritative state and advances the shared durable
 /// revision. A stale writer cannot overwrite a state transition committed
 /// by another instance.
 /// </summary>
 public AuthorizationRecoveryCrossInstanceResult TryPublish(
 string expectedActiveKeyId,
 long expectedRevision,
 AuthorizationRecoveryControlPlanePublication publication)
 {
 ArgumentNullException.ThrowIfNull(publication);

 lock (_durableGate)
 {
 if (expectedRevision != _state.Revision ||
 !string.Equals(
 expectedActiveKeyId,
 _state.KeyRing.ActiveKeyId,
 StringComparison.Ordinal))
 {
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;
 }

 if (!_state.KeyRing.Keys.TryGetValue(
 expectedActiveKeyId, out var key) ||
 key.Status != AuthorizationRecoveryKeyStatus.Active)
 {
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;
 }

 if (publication.Sequence < _state.Publication.Sequence ||
 !string.Equals(
 publication.IntegrityKeyId,
 expectedActiveKeyId,
 StringComparison.Ordinal))
 {
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;
 }

 var material = _keyResolver(expectedActiveKeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
 publication with
 {
 IntegrityKeyId = expectedActiveKeyId
 }, material))
 {
 return AuthorizationRecoveryCrossInstanceResult.PublicationRejected;
 }

 _state = _state with
 {
 Revision = checked(_state.Revision + 1),
 Publication = publication
 };

 return AuthorizationRecoveryCrossInstanceResult.PublicationCommitted;
 }
 }

}
