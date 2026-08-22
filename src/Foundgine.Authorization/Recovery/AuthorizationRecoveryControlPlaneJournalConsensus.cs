using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

public enum AuthorizationRecoveryJournalConsensusResult
{
 ConsensusEstablished,
 RejectedInvalidJournal,
 DivergentHistory,
 DivergentState,
 ConflictingCommittedTransaction,
 StaleReplica,
 NoAuthoritativeConsensus
}

public sealed record AuthorizationRecoveryJournalReplicaSnapshot(
 string InstanceId,
 long DurableRevision,
 string StateFingerprint,
 IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> JournalEntries);

public sealed record AuthorizationRecoveryJournalConsensus(
 long Revision,
 string StateFingerprint,
 string JournalHeadDigest,
 string LastCommittedTransactionId,
 IReadOnlyList<string> ParticipatingInstances);

/// <summary>
/// reference model for detecting divergent transaction journals across
/// control-plane instances. It never elects an arbitrary winner. Consensus is
/// established only when authenticated journal history and durable state agree.
/// Divergence is a security failure and must be reconciled by an authoritative
/// external durable mechanism before recovery can continue.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneJournalConsensus
{
 private readonly byte[] _journalKey;
 private readonly object _gate = new();
 private AuthorizationRecoveryJournalConsensus? _consensus;

 public AuthorizationRecoveryControlPlaneJournalConsensus(ReadOnlySpan<byte> journalKey)
 {
 if (journalKey.Length < 16)
 throw new ArgumentException("Journal consensus key must be at least 128 bits.", nameof(journalKey));
 _journalKey = journalKey.ToArray();
 }

 public AuthorizationRecoveryJournalConsensus? Current
 {
 get { lock (_gate) return _consensus; }
 }

 public AuthorizationRecoveryJournalConsensusResult TryEstablishConsensus(
 IReadOnlyList<AuthorizationRecoveryJournalReplicaSnapshot> replicas,
 out AuthorizationRecoveryJournalConsensus? consensus)
 {
 ArgumentNullException.ThrowIfNull(replicas);
 lock (_gate)
 {
 consensus = null;
 _consensus = null;

 if (replicas.Count < 2)
 return AuthorizationRecoveryJournalConsensusResult.NoAuthoritativeConsensus;

 var ids = new HashSet<string>(StringComparer.Ordinal);
 foreach (var replica in replicas)
 {
 if (string.IsNullOrWhiteSpace(replica.InstanceId) || !ids.Add(replica.InstanceId))
 return AuthorizationRecoveryJournalConsensusResult.DivergentState;

 if (!VerifyChain(replica.JournalEntries))
 return AuthorizationRecoveryJournalConsensusResult.RejectedInvalidJournal;
 }

 var first = replicas[0];
 if (replicas.Any(r => r.DurableRevision != first.DurableRevision))
 return AuthorizationRecoveryJournalConsensusResult.DivergentState;

 if (replicas.Any(r => !string.Equals(r.StateFingerprint, first.StateFingerprint, StringComparison.Ordinal)))
 return AuthorizationRecoveryJournalConsensusResult.DivergentState;

 var firstHead = HeadDigest(first.JournalEntries);
 if (replicas.Any(r => !string.Equals(HeadDigest(r.JournalEntries), firstHead, StringComparison.Ordinal)))
 return AuthorizationRecoveryJournalConsensusResult.DivergentHistory;

 var lastCommitted = LastCommittedTransaction(first.JournalEntries);
 if (replicas.Any(r => !string.Equals(LastCommittedTransaction(r.JournalEntries), lastCommitted, StringComparison.Ordinal)))
 return AuthorizationRecoveryJournalConsensusResult.ConflictingCommittedTransaction;

 consensus = new AuthorizationRecoveryJournalConsensus(
 first.DurableRevision,
 first.StateFingerprint,
 firstHead,
 lastCommitted,
 replicas.Select(r => r.InstanceId).OrderBy(x => x, StringComparer.Ordinal).ToArray());

 _consensus = consensus;
 return AuthorizationRecoveryJournalConsensusResult.ConsensusEstablished;
 }
 }

 /// <summary>
 /// A replica may only recover from a consensus established over the exact
 /// durable revision, state fingerprint, and journal head it observed.
 /// </summary>
 public AuthorizationRecoveryJournalConsensusResult ValidateRecoveryAgainstConsensus(
 AuthorizationRecoveryJournalReplicaSnapshot replica)
 {
 ArgumentNullException.ThrowIfNull(replica);
 lock (_gate)
 {
 if (_consensus is null)
 return AuthorizationRecoveryJournalConsensusResult.NoAuthoritativeConsensus;

 if (replica.DurableRevision != _consensus.Revision ||
 !string.Equals(replica.StateFingerprint, _consensus.StateFingerprint, StringComparison.Ordinal) ||
 !string.Equals(HeadDigest(replica.JournalEntries), _consensus.JournalHeadDigest, StringComparison.Ordinal))
 return AuthorizationRecoveryJournalConsensusResult.StaleReplica;

 if (!VerifyChain(replica.JournalEntries))
 return AuthorizationRecoveryJournalConsensusResult.RejectedInvalidJournal;

 return AuthorizationRecoveryJournalConsensusResult.ConsensusEstablished;
 }
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

 private static string HeadDigest(IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries) =>
 entries.Count == 0 ? string.Empty : entries[^1].Digest;

 private static string LastCommittedTransaction(IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries) =>
 entries.LastOrDefault(e => e.Phase == AuthorizationRecoveryDurableCommitPhase.Committed)?.TransactionId ?? string.Empty;
}
