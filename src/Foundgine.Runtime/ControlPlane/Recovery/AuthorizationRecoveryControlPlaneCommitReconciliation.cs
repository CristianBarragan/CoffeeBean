namespace Foundgine.Runtime.ControlPlane;

public enum AuthorizationRecoveryCommitReconciliationResult
{
 Prepared,
 Committed,
 RecoveredCommittedOutcome,
 AbortedPreparedOutcome,
 RejectedStaleState,
 RejectedRetiredKey,
 RejectedIntegrity,
 RejectedActiveKey,
 RejectedPublication,
 NoPendingTransaction,
 ConflictDetected
}

public enum AuthorizationRecoveryCommitCrashPoint
{
 None,
 BeforePrepareDurableWrite,
 AfterPrepareBeforeApply,
 AfterApplyBeforeCommitAcknowledgement
}

public enum AuthorizationRecoveryDurableCommitPhase
{
 Prepared,
 Committed
}

/// <summary>
/// reference model for recovery of an unresolved cross-instance commit.
/// A durable transaction record is written before mutation and marked committed
/// only after the complete state transition is durable. Restart/reconciliation
/// never guesses an unknown outcome: it derives the outcome from the durable
/// transaction record and the state revision/transaction identity.
/// </summary>
public sealed record AuthorizationRecoveryDurableCommitRecord(
 string TransactionId,
 long BaseRevision,
 long TargetRevision,
 string Operation,
 AuthorizationRecoveryDurableCommitPhase Phase,
 string TargetFingerprint);

public sealed record AuthorizationRecoveryReconciledState(
 AuthorizationRecoveryCrossInstanceState State,
 string LastCommittedTransactionId,
 AuthorizationRecoveryDurableCommitRecord? PendingTransaction);

public sealed class AuthorizationRecoveryControlPlaneCommitReconciliation
{
 private readonly object _durableGate = new();
 private readonly Func<string, byte[]?> _keyResolver;
 private readonly long _verificationWindowSequences;
 private AuthorizationRecoveryCrossInstanceState _state;
 private string _lastCommittedTransactionId = string.Empty;
 private AuthorizationRecoveryDurableCommitRecord? _pending;

 public AuthorizationRecoveryControlPlaneCommitReconciliation(
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

 public AuthorizationRecoveryReconciledState Current
 {
 get
 {
 lock (_durableGate)
 {
 return new(_state, _lastCommittedTransactionId, _pending);
 }
 }
 }

 public AuthorizationRecoveryCommitReconciliationResult TryPrepareHistoricalRecovery(
 AuthorizationRecoveryControlPlanePublication historicalPublication,
 string transactionId,
 out AuthorizationRecoveryDurableCommitRecord? transaction)
 {
 ArgumentNullException.ThrowIfNull(historicalPublication);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 transaction = null;
 if (_pending is not null)
 return AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;

 if (!_state.KeyRing.Keys.TryGetValue(historicalPublication.IntegrityKeyId, out var key) ||
 key.Status == AuthorizationRecoveryKeyStatus.Retired)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedRetiredKey;

 // The verification window governs when a VerificationOnly generation
 // may be retired (see TryPrepareRetirement / TryRetire below), not
 // whether a historical publication can be recovered. As long as the
 // signing generation has not been retired, its historical publication
 // remains verifiable; there is no separate sequence-staleness gate here.
 var material = _keyResolver(historicalPublication.IntegrityKeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(historicalPublication, material))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedIntegrity;

 transaction = new AuthorizationRecoveryDurableCommitRecord(
 transactionId,
 _state.Revision,
 checked(_state.Revision + 1),
 "historical-recovery",
 AuthorizationRecoveryDurableCommitPhase.Prepared,
 Fingerprint(historicalPublication));

 _pending = transaction;
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;
 }
 }

 /// <summary>
 /// Simulates execution of the prepared transaction. A crash after apply but
 /// before acknowledgement is intentionally represented as an unresolved
 /// durable transaction; reconciliation must recover it as committed.
 /// </summary>
 public AuthorizationRecoveryCommitReconciliationResult ExecutePreparedRecovery(
 AuthorizationRecoveryControlPlanePublication historicalPublication,
 string transactionId,
 AuthorizationRecoveryCommitCrashPoint crashPoint = AuthorizationRecoveryCommitCrashPoint.None)
 {
 ArgumentNullException.ThrowIfNull(historicalPublication);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 if (_pending is null ||
 !string.Equals(_pending.TransactionId, transactionId, StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction;

 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterPrepareBeforeApply)
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;

 if (_state.Revision != _pending.BaseRevision)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedStaleState;

 var material = _keyResolver(historicalPublication.IntegrityKeyId);
 if (material is null ||
 !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(historicalPublication, material))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedIntegrity;

 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.BeforePrepareDurableWrite)
 return AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome;

 _state = _state with { Revision = _pending.TargetRevision };

 _pending = _pending with { Phase = AuthorizationRecoveryDurableCommitPhase.Committed };
 _lastCommittedTransactionId = transactionId;

 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement)
 return AuthorizationRecoveryCommitReconciliationResult.Committed;

 _pending = null;
 return AuthorizationRecoveryCommitReconciliationResult.Committed;
 }
 }

 /// <summary>
 /// Reconciles a transaction left behind by process death. Prepared means the
 /// state transition never crossed the durable commit boundary and is safely
 /// discarded. Committed means the durable outcome already crossed it and is
 /// acknowledged without replaying the security transition.
 /// </summary>
 public AuthorizationRecoveryCommitReconciliationResult Reconcile()
 {
 lock (_durableGate)
 {
 if (_pending is null)
 return AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction;

 if (_pending.Phase == AuthorizationRecoveryDurableCommitPhase.Committed)
 {
 if (_state.Revision != _pending.TargetRevision ||
 !string.Equals(_lastCommittedTransactionId, _pending.TransactionId, StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;

 _pending = null;
 return AuthorizationRecoveryCommitReconciliationResult.RecoveredCommittedOutcome;
 }

 if (_state.Revision != _pending.BaseRevision)
 return AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;

 _pending = null;
 return AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome;
 }
 }

 public AuthorizationRecoveryCommitReconciliationResult TryPrepareRetirement(
 string keyId,
 string expectedActiveKeyId,
 long expectedSequence,
 string transactionId,
 out AuthorizationRecoveryDurableCommitRecord? transaction)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(expectedActiveKeyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 transaction = null;
 if (_pending is not null)
 return AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;

 if (expectedSequence != _state.Publication.Sequence ||
 !string.Equals(expectedActiveKeyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedStaleState;

 if (!_state.KeyRing.Keys.TryGetValue(keyId, out var key) ||
 key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly ||
 string.Equals(keyId, expectedActiveKeyId, StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedRetiredKey;

 var protectedFrom = _state.Publication.Sequence - _verificationWindowSequences;
 if (string.Equals(_state.Publication.IntegrityKeyId, keyId, StringComparison.Ordinal) &&
 _state.Publication.Sequence >= protectedFrom)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedStaleState;

 transaction = new(
 transactionId,
 _state.Revision,
 checked(_state.Revision + 1),
 "key-retirement",
 AuthorizationRecoveryDurableCommitPhase.Prepared,
 $"retire:{keyId}:{expectedActiveKeyId}:{expectedSequence}");
 _pending = transaction;
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;
 }
 }

 public AuthorizationRecoveryCommitReconciliationResult ExecutePreparedRetirement(
 string keyId,
 string transactionId,
 AuthorizationRecoveryCommitCrashPoint crashPoint = AuthorizationRecoveryCommitCrashPoint.None)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 if (_pending is null || !string.Equals(_pending.TransactionId, transactionId, StringComparison.Ordinal) ||
 !string.Equals(_pending.Operation, "key-retirement", StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction;

 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterPrepareBeforeApply)
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;
 if (_state.Revision != _pending.BaseRevision)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedStaleState;
 if (!_state.KeyRing.Keys.TryGetValue(keyId, out var key) ||
 key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedRetiredKey;
 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.BeforePrepareDurableWrite)
 return AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome;

 var nextKeys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(_state.KeyRing.Keys, StringComparer.Ordinal)
 {
 [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
 };
 _state = _state with
 {
 Revision = _pending.TargetRevision,
 KeyRing = new AuthorizationRecoveryKeyRing(_state.KeyRing.ActiveKeyId, nextKeys)
 };
 _pending = _pending with { Phase = AuthorizationRecoveryDurableCommitPhase.Committed };
 _lastCommittedTransactionId = transactionId;
 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement)
 return AuthorizationRecoveryCommitReconciliationResult.Committed;
 _pending = null;
 return AuthorizationRecoveryCommitReconciliationResult.Committed;
 }
 }

 public AuthorizationRecoveryCommitReconciliationResult TryPreparePublication(
 string expectedActiveKeyId,
 AuthorizationRecoveryControlPlanePublication publication,
 string transactionId,
 out AuthorizationRecoveryDurableCommitRecord? transaction)
 {
 ArgumentNullException.ThrowIfNull(publication);
 ArgumentException.ThrowIfNullOrWhiteSpace(expectedActiveKeyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 transaction = null;
 if (_pending is not null)
 return AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;
 if (!string.Equals(expectedActiveKeyId, _state.KeyRing.ActiveKeyId, StringComparison.Ordinal) ||
 publication.Sequence < _state.Publication.Sequence ||
 !string.Equals(publication.IntegrityKeyId, expectedActiveKeyId, StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedPublication;
 if (!_state.KeyRing.Keys.TryGetValue(expectedActiveKeyId, out var key) ||
 key.Status != AuthorizationRecoveryKeyStatus.Active)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedActiveKey;

 var material = _keyResolver(expectedActiveKeyId);
 if (material is null || !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(publication, material))
 return AuthorizationRecoveryCommitReconciliationResult.RejectedIntegrity;

 transaction = new(
 transactionId,
 _state.Revision,
 checked(_state.Revision + 1),
 "authoritative-publication",
 AuthorizationRecoveryDurableCommitPhase.Prepared,
 Fingerprint(publication));
 _pending = transaction;
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;
 }
 }

 public AuthorizationRecoveryCommitReconciliationResult ExecutePreparedPublication(
 AuthorizationRecoveryControlPlanePublication publication,
 string transactionId,
 AuthorizationRecoveryCommitCrashPoint crashPoint = AuthorizationRecoveryCommitCrashPoint.None)
 {
 ArgumentNullException.ThrowIfNull(publication);
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

 lock (_durableGate)
 {
 if (_pending is null || !string.Equals(_pending.TransactionId, transactionId, StringComparison.Ordinal) ||
 !string.Equals(_pending.Operation, "authoritative-publication", StringComparison.Ordinal))
 return AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction;
 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterPrepareBeforeApply)
 return AuthorizationRecoveryCommitReconciliationResult.Prepared;
 if (_state.Revision != _pending.BaseRevision)
 return AuthorizationRecoveryCommitReconciliationResult.RejectedStaleState;
 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.BeforePrepareDurableWrite)
 return AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome;

 _state = _state with { Revision = _pending.TargetRevision, Publication = publication };
 _pending = _pending with { Phase = AuthorizationRecoveryDurableCommitPhase.Committed };
 _lastCommittedTransactionId = transactionId;
 if (crashPoint == AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement)
 return AuthorizationRecoveryCommitReconciliationResult.Committed;
 _pending = null;
 return AuthorizationRecoveryCommitReconciliationResult.Committed;
 }
 }

 /// <summary>
 /// Models a stale instance attempting to start work while an unresolved
 /// transaction exists. Reconciliation must happen first; no second writer
 /// is allowed to bypass the durable transaction fence.
 /// </summary>
 public AuthorizationRecoveryCommitReconciliationResult RejectIfUnreconciled()
 {
 lock (_durableGate)
 return _pending is null
 ? AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction
 : AuthorizationRecoveryCommitReconciliationResult.ConflictDetected;
 }

 private static string Fingerprint(AuthorizationRecoveryControlPlanePublication publication) =>
 $"{publication.Epoch}:{publication.Sequence}:{publication.IntegrityKeyId}:{publication.HeadDigest}";
}
