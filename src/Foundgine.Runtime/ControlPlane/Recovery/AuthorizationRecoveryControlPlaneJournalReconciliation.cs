using System.Security.Cryptography;

namespace Foundgine.Runtime.ControlPlane;

public enum AuthorizationRecoveryJournalReconciliationResult
{
 Reconciled,
 AlreadySynchronized,
 RejectedInvalidLocalJournal,
 RejectedInvalidAuthoritativeJournal,
 ConflictingHistory,
 StateMismatch,
 RevisionMismatch,
 InvalidAuthoritativeTransition
}

public sealed record AuthorizationRecoveryJournalRepairPlan(
 string InstanceId,
 long ExpectedLocalRevision,
 string ExpectedLocalHeadDigest,
 long TargetRevision,
 string TargetStateFingerprint,
 string TargetJournalHeadDigest,
 string TargetLastCommittedTransactionId,
 IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> AuthoritativeEntries);

/// <summary>
/// reference model. Reconciles a stale replica only against an explicitly
/// supplied authoritative durable history. A local divergent fork is never
/// overwritten implicitly. The result is a repair plan that must be committed
/// atomically by the authoritative control-plane transaction.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneJournalReconciliation
{
 private readonly byte[] _journalKey;

 public AuthorizationRecoveryControlPlaneJournalReconciliation(ReadOnlySpan<byte> journalKey)
 {
 if (journalKey.Length < 16)
 throw new ArgumentException("Journal reconciliation key must be at least 128 bits.", nameof(journalKey));
 _journalKey = journalKey.ToArray();
 }

 public AuthorizationRecoveryJournalReconciliationResult TryBuildRepairPlan(
 AuthorizationRecoveryJournalReplicaSnapshot local,
 AuthorizationRecoveryJournalReplicaSnapshot authoritative,
 out AuthorizationRecoveryJournalRepairPlan? plan)
 {
 ArgumentNullException.ThrowIfNull(local);
 ArgumentNullException.ThrowIfNull(authoritative);
 plan = null;

 if (!VerifyChain(local.JournalEntries))
 return AuthorizationRecoveryJournalReconciliationResult.RejectedInvalidLocalJournal;

 if (!VerifyChain(authoritative.JournalEntries))
 return AuthorizationRecoveryJournalReconciliationResult.RejectedInvalidAuthoritativeJournal;

 if (authoritative.DurableRevision < 0 || local.DurableRevision < 0)
 return AuthorizationRecoveryJournalReconciliationResult.RevisionMismatch;

 if (local.DurableRevision == authoritative.DurableRevision &&
 string.Equals(local.StateFingerprint, authoritative.StateFingerprint, StringComparison.Ordinal) &&
 string.Equals(HeadDigest(local.JournalEntries), HeadDigest(authoritative.JournalEntries), StringComparison.Ordinal))
 return AuthorizationRecoveryJournalReconciliationResult.AlreadySynchronized;

 // The authoritative history must contain the complete local history as a
 // prefix. Equal sequence numbers with different entries are a fork, not
 // stale state that may safely be overwritten.
 if (local.JournalEntries.Count > authoritative.JournalEntries.Count)
 return AuthorizationRecoveryJournalReconciliationResult.ConflictingHistory;

 for (var i = 0; i < local.JournalEntries.Count; i++)
 {
 if (!EntriesEquivalent(local.JournalEntries[i], authoritative.JournalEntries[i]))
 return AuthorizationRecoveryJournalReconciliationResult.ConflictingHistory;
 }

 if (local.DurableRevision > authoritative.DurableRevision)
 return AuthorizationRecoveryJournalReconciliationResult.RevisionMismatch;

 // A replica may only move to the authoritative state. If it has the same
 // history length but a different state fingerprint, the state transition
 // cannot be inferred from journal shape alone.
 if (local.JournalEntries.Count == authoritative.JournalEntries.Count)
 return AuthorizationRecoveryJournalReconciliationResult.StateMismatch;

 plan = new AuthorizationRecoveryJournalRepairPlan(
 local.InstanceId,
 local.DurableRevision,
 HeadDigest(local.JournalEntries),
 authoritative.DurableRevision,
 authoritative.StateFingerprint,
 HeadDigest(authoritative.JournalEntries),
 LastCommittedTransaction(authoritative.JournalEntries),
 authoritative.JournalEntries.ToArray());

 return AuthorizationRecoveryJournalReconciliationResult.Reconciled;
 }

 private bool VerifyChain(IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries)
 {
 string previous = string.Empty;
 long expectedSequence = 1;
 foreach (var entry in entries)
 {
 if (entry.JournalSequence != expectedSequence ||
 !string.Equals(entry.PreviousDigest, previous, StringComparison.Ordinal) ||
 !AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.VerifyEntry(entry, _journalKey))
 return false;

 previous = entry.Digest;
 expectedSequence++;
 }
 return true;
 }

 private static bool EntriesEquivalent(
 AuthorizationRecoveryTransactionJournalEntry left,
 AuthorizationRecoveryTransactionJournalEntry right) =>
 left.JournalSequence == right.JournalSequence &&
 string.Equals(left.TransactionId, right.TransactionId, StringComparison.Ordinal) &&
 left.BaseRevision == right.BaseRevision &&
 left.TargetRevision == right.TargetRevision &&
 string.Equals(left.Operation, right.Operation, StringComparison.Ordinal) &&
 left.Phase == right.Phase &&
 string.Equals(left.TargetFingerprint, right.TargetFingerprint, StringComparison.Ordinal) &&
 string.Equals(left.PreviousDigest, right.PreviousDigest, StringComparison.Ordinal) &&
 string.Equals(left.Digest, right.Digest, StringComparison.Ordinal) &&
 string.Equals(left.AuthenticationTag, right.AuthenticationTag, StringComparison.Ordinal);

 private static string HeadDigest(IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries) =>
 entries.Count == 0 ? string.Empty : entries[^1].Digest;

 private static string LastCommittedTransaction(IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries) =>
 entries.LastOrDefault(e => e.Phase == AuthorizationRecoveryDurableCommitPhase.Committed)?.TransactionId ?? string.Empty;
}
