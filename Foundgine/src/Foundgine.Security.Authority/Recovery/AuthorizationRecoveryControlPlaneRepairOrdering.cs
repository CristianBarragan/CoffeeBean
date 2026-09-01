namespace Foundgine.Security.Authority;

public enum AuthorizationRecoveryRepairCommitResult
{
 Committed,
 AlreadyCommitted,
 RejectedIdentityCollision,
 RejectedStalePlan,
 RejectedOrdering,
 RejectedTarget,
 RejectedJournal
}

/// <summary>
/// reference model for cross-instance repair ordering and transaction
/// identity safety. A repair transaction is idempotent only when every
/// immutable identity field matches the original committed transaction.
/// Ordering is monotonic: a repair may advance exactly one revision from the
/// current durable revision and may never be inserted behind an existing head.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairOrdering
{
 private readonly object _gate = new();
 private readonly Dictionary<string, RepairCommitIdentity> _committed = new(StringComparer.Ordinal);
 private long _revision;
 private string _stateFingerprint;
 private string _journalHead;

 public AuthorizationRecoveryControlPlaneRepairOrdering(long initialRevision, string initialStateFingerprint, string initialJournalHead = "")
 {
 if (initialRevision < 0) throw new ArgumentOutOfRangeException(nameof(initialRevision));
 ArgumentException.ThrowIfNullOrWhiteSpace(initialStateFingerprint);
 _revision = initialRevision;
 _stateFingerprint = initialStateFingerprint;
 _journalHead = initialJournalHead ?? string.Empty;
 }

 public (long Revision, string StateFingerprint, string JournalHead) Snapshot()
 {
 lock (_gate) return (_revision, _stateFingerprint, _journalHead);
 }

 public AuthorizationRecoveryRepairCommitResult Commit(
 string transactionId,
 long expectedRevision,
 string expectedStateFingerprint,
 string expectedJournalHead,
 long targetRevision,
 string targetStateFingerprint,
 string targetJournalHead)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
 ArgumentException.ThrowIfNullOrWhiteSpace(expectedStateFingerprint);
 ArgumentException.ThrowIfNullOrWhiteSpace(targetStateFingerprint);
 expectedJournalHead ??= string.Empty;
 targetJournalHead ??= string.Empty;

 var identity = new RepairCommitIdentity(
 expectedRevision, expectedStateFingerprint, expectedJournalHead,
 targetRevision, targetStateFingerprint, targetJournalHead);

 lock (_gate)
 {
 if (_committed.TryGetValue(transactionId, out var prior))
 return prior == identity
 ? AuthorizationRecoveryRepairCommitResult.AlreadyCommitted
 : AuthorizationRecoveryRepairCommitResult.RejectedIdentityCollision;

 if (_revision != expectedRevision ||
 !string.Equals(_stateFingerprint, expectedStateFingerprint, StringComparison.Ordinal) ||
 !string.Equals(_journalHead, expectedJournalHead, StringComparison.Ordinal))
 return AuthorizationRecoveryRepairCommitResult.RejectedStalePlan;

 if (targetRevision != expectedRevision + 1)
 return AuthorizationRecoveryRepairCommitResult.RejectedOrdering;

 if (string.Equals(targetStateFingerprint, expectedStateFingerprint, StringComparison.Ordinal) &&
 string.Equals(targetJournalHead, expectedJournalHead, StringComparison.Ordinal))
 return AuthorizationRecoveryRepairCommitResult.RejectedTarget;

 _revision = targetRevision;
 _stateFingerprint = targetStateFingerprint;
 _journalHead = targetJournalHead;
 _committed.Add(transactionId, identity);
 return AuthorizationRecoveryRepairCommitResult.Committed;
 }
 }

 private sealed record RepairCommitIdentity(
 long ExpectedRevision,
 string ExpectedStateFingerprint,
 string ExpectedJournalHead,
 long TargetRevision,
 string TargetStateFingerprint,
 string TargetJournalHead);
}
