using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>
/// tamper-evident durable transaction journal reference model.
/// Journal entries are chained and authenticated independently of the mutable
/// control-plane state. A transaction record is accepted for reconciliation
/// only when its predecessor, sequence, immutable fields, and authentication
/// tag all verify.
/// </summary>
public sealed record AuthorizationRecoveryTransactionJournalEntry(
 long JournalSequence,
 string TransactionId,
 long BaseRevision,
 long TargetRevision,
 string Operation,
 AuthorizationRecoveryDurableCommitPhase Phase,
 string TargetFingerprint,
 string PreviousDigest,
 string Digest,
 string AuthenticationTag);

public enum AuthorizationRecoveryTransactionJournalResult
{
 Accepted,
 Duplicate,
 RejectedSequence,
 RejectedChain,
 RejectedDigest,
 RejectedAuthentication,
 RejectedTransaction,
 RejectedPhaseTransition,
 RejectedRevision,
 RejectedFingerprint,
 RejectedReplay
}

public static class AuthorizationRecoveryControlPlaneTransactionJournalIntegrity
{
 public const string SupportedAlgorithm = "HMAC-SHA256/JOURNAL-v1";

 public static string ComputeDigest(AuthorizationRecoveryTransactionJournalEntry entry)
 {
 var canonical = Canonicalize(
 entry.JournalSequence, entry.TransactionId, entry.BaseRevision,
 entry.TargetRevision, entry.Operation, entry.Phase,
 entry.TargetFingerprint, entry.PreviousDigest);
 return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
 }

 public static string ComputeAuthenticationTag(
 AuthorizationRecoveryTransactionJournalEntry entry,
 ReadOnlySpan<byte> key)
 {
 using var hmac = new HMACSHA256(key.ToArray());
 return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(entry.Digest)));
 }

 public static bool VerifyEntry(
 AuthorizationRecoveryTransactionJournalEntry entry,
 ReadOnlySpan<byte> key)
 {
 if (!string.Equals(entry.Digest, ComputeDigest(entry), StringComparison.Ordinal))
 return false;

 return VerifyAuthenticationTag(entry, key);
 }

 /// <summary>
 /// Verifies only the HMAC tag over the entry's (already-trusted) digest,
 /// without recomputing the digest itself. Kept separate from
 /// <see cref="VerifyEntry"/> so a caller that needs to distinguish
 /// "content/digest was tampered with" from "the authentication tag itself
 /// is wrong" (e.g. wrong signing key) can report the correct one instead
 /// of a single combined result.
 /// </summary>
 public static bool VerifyAuthenticationTag(
 AuthorizationRecoveryTransactionJournalEntry entry,
 ReadOnlySpan<byte> key)
 {
 using var hmac = new HMACSHA256(key.ToArray());
 var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(entry.Digest));
 try
 {
 return CryptographicOperations.FixedTimeEquals(
 Convert.FromHexString(entry.AuthenticationTag), expected);
 }
 catch (FormatException)
 {
 return false;
 }
 }

 private static string Canonicalize(
 long sequence,
 string transactionId,
 long baseRevision,
 long targetRevision,
 string operation,
 AuthorizationRecoveryDurableCommitPhase phase,
 string fingerprint,
 string previousDigest)
 {
 static string Field(string value)
 {
 var bytes = Encoding.UTF8.GetBytes(value);
 return $"{bytes.Length}:{value}";
 }

 return string.Concat(
 Field(SupportedAlgorithm),
 Field(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
 Field(transactionId),
 Field(baseRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
 Field(targetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
 Field(operation),
 Field(phase.ToString()),
 Field(fingerprint),
 Field(previousDigest));
 }
}

/// <summary>
/// Append-only journal model. It refuses gaps, mutation of committed entries,
/// phase regression, replay of a transaction with different immutable data,
/// and entries authenticated by the wrong journal key.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneTransactionJournal
{
 private readonly object _gate = new();
 private readonly byte[] _authenticationKey;
 private readonly List<AuthorizationRecoveryTransactionJournalEntry> _entries = new();

 public AuthorizationRecoveryControlPlaneTransactionJournal(ReadOnlySpan<byte> authenticationKey)
 {
 if (authenticationKey.Length < 16)
 throw new ArgumentException("Journal authentication key must be at least 128 bits.", nameof(authenticationKey));
 _authenticationKey = authenticationKey.ToArray();
 }

 public IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> Entries
 {
 get { lock (_gate) return _entries.ToArray(); }
 }

 public AuthorizationRecoveryTransactionJournalResult Append(
 string transactionId,
 long baseRevision,
 long targetRevision,
 string operation,
 AuthorizationRecoveryDurableCommitPhase phase,
 string targetFingerprint)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
 ArgumentException.ThrowIfNullOrWhiteSpace(operation);
 ArgumentException.ThrowIfNullOrWhiteSpace(targetFingerprint);

 lock (_gate)
 {
 if (targetRevision != baseRevision + 1)
 return AuthorizationRecoveryTransactionJournalResult.RejectedRevision;

 var existing = _entries.FirstOrDefault(x =>
 string.Equals(x.TransactionId, transactionId, StringComparison.Ordinal));

 if (existing is not null)
 {
 if (existing.BaseRevision != baseRevision ||
 existing.TargetRevision != targetRevision ||
 !string.Equals(existing.Operation, operation, StringComparison.Ordinal) ||
 !string.Equals(existing.TargetFingerprint, targetFingerprint, StringComparison.Ordinal))
 return AuthorizationRecoveryTransactionJournalResult.RejectedReplay;

 if (existing.Phase == AuthorizationRecoveryDurableCommitPhase.Committed &&
 phase == AuthorizationRecoveryDurableCommitPhase.Prepared)
 return AuthorizationRecoveryTransactionJournalResult.RejectedPhaseTransition;

 if (existing.Phase == phase)
 return AuthorizationRecoveryTransactionJournalResult.Duplicate;
 }

 var sequence = _entries.Count == 0 ? 1 : _entries[^1].JournalSequence + 1;
 var previousDigest = _entries.Count == 0 ? string.Empty : _entries[^1].Digest;
 var unsigned = new AuthorizationRecoveryTransactionJournalEntry(
 sequence, transactionId, baseRevision, targetRevision, operation,
 phase, targetFingerprint, previousDigest, string.Empty, string.Empty);
 var digest = AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.ComputeDigest(unsigned);
 var authenticated = unsigned with { Digest = digest };
 var tag = AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.ComputeAuthenticationTag(authenticated, _authenticationKey);
 _entries.Add(authenticated with { AuthenticationTag = tag });
 return AuthorizationRecoveryTransactionJournalResult.Accepted;
 }
 }

 public AuthorizationRecoveryTransactionJournalResult VerifyChain()
 {
 lock (_gate)
 {
 string previous = string.Empty;
 long expectedSequence = 1;
 var seen = new HashSet<string>(StringComparer.Ordinal);

 foreach (var entry in _entries)
 {
 if (entry.JournalSequence != expectedSequence)
 return AuthorizationRecoveryTransactionJournalResult.RejectedSequence;
 if (!string.Equals(entry.PreviousDigest, previous, StringComparison.Ordinal))
 return AuthorizationRecoveryTransactionJournalResult.RejectedChain;
 if (!seen.Add(entry.TransactionId))
 {
 // Duplicate transaction IDs are allowed only as a legitimate
 // Prepared -> Committed transition.
 var same = _entries.Where(x => x.TransactionId == entry.TransactionId).ToArray();
 if (same.Length != 2 || same[0].Phase != AuthorizationRecoveryDurableCommitPhase.Prepared ||
 same[1].Phase != AuthorizationRecoveryDurableCommitPhase.Committed)
 return AuthorizationRecoveryTransactionJournalResult.RejectedReplay;
 }
 if (!AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.VerifyEntry(entry, _authenticationKey))
 return AuthorizationRecoveryTransactionJournalResult.RejectedAuthentication;
 if (entry.TargetRevision != entry.BaseRevision + 1)
 return AuthorizationRecoveryTransactionJournalResult.RejectedRevision;

 previous = entry.Digest;
 expectedSequence++;
 }

 return AuthorizationRecoveryTransactionJournalResult.Accepted;
 }
 }

 public AuthorizationRecoveryTransactionJournalResult VerifyEntry(
 AuthorizationRecoveryTransactionJournalEntry entry)
 {
 lock (_gate)
 {
 // Check the authentication tag first: it is computed only over the
 // entry's Digest field, so it still verifies correctly even when
 // other content (e.g. TargetFingerprint, JournalSequence) has been
 // tampered with independently of Digest. That lets us distinguish
 // "the tag/key itself is wrong" (RejectedAuthentication) from
 // "the content no longer matches its own digest" (RejectedDigest)
 // instead of collapsing both into one result.
 if (!AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.VerifyAuthenticationTag(entry, _authenticationKey))
 return AuthorizationRecoveryTransactionJournalResult.RejectedAuthentication;

 if (!string.Equals(
 entry.Digest,
 AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.ComputeDigest(entry),
 StringComparison.Ordinal))
 return AuthorizationRecoveryTransactionJournalResult.RejectedDigest;

 if (entry.JournalSequence < 1 || entry.JournalSequence > _entries.Count)
 return AuthorizationRecoveryTransactionJournalResult.RejectedSequence;

 var stored = _entries[(int)entry.JournalSequence - 1];
 if (!Equals(stored, entry))
 return AuthorizationRecoveryTransactionJournalResult.RejectedDigest;

 return AuthorizationRecoveryTransactionJournalResult.Accepted;
 }
 }
}