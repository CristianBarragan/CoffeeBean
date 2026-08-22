namespace Foundgine.Authorization;

/// <summary>
/// Durable persistence boundary for the reconfiguration ledger plus the witness-id
/// manifest needed to reconstruct runtime membership after restart. Implementations must make
/// the record and its membership manifest durable as one atomic append.
/// </summary>
public interface IAuthorizationRecoveryReconfigurationLedgerStore
{
 ValueTask<AuthorizationRecoveryReconfigurationLedgerSnapshot> LoadAsync(
 CancellationToken cancellationToken = default);

 ValueTask AppendAsync(
 AuthorizationRecoveryReconfigurationAuditRecord record,
 IReadOnlyList<string> witnessIds,
 CancellationToken cancellationToken = default);
}

public sealed record AuthorizationRecoveryReconfigurationLedgerSnapshot(
 IReadOnlyList<AuthorizationRecoveryReconfigurationAuditRecord> Records,
 IReadOnlyDictionary<long, IReadOnlyList<string>> MembershipByVersion);

/// <summary>
/// Reference implementation for adversarial tests. It is process-local and is not a
/// production rollback-resistant store.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryReconfigurationLedgerStore
 : IAuthorizationRecoveryReconfigurationLedgerStore
{
 private readonly object _gate = new();
 private readonly List<AuthorizationRecoveryReconfigurationAuditRecord> _records = new();
 private readonly Dictionary<long, IReadOnlyList<string>> _memberships = new();

 public ValueTask<AuthorizationRecoveryReconfigurationLedgerSnapshot> LoadAsync(
 CancellationToken cancellationToken = default)
 {
 lock (_gate)
 {
 return ValueTask.FromResult(new AuthorizationRecoveryReconfigurationLedgerSnapshot(
 _records.ToArray(),
 _memberships.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value.ToArray())));
 }
 }

 public ValueTask AppendAsync(
 AuthorizationRecoveryReconfigurationAuditRecord record,
 IReadOnlyList<string> witnessIds,
 CancellationToken cancellationToken = default)
 {
 if (witnessIds is null || witnessIds.Count == 0)
 throw new ArgumentException("Witness ids are required.", nameof(witnessIds));

 lock (_gate)
 {
 var current = _records.ToArray();
 var verification = AuthorizationRecoveryReconfigurationLedger.VerifyChain(current);
 if (!verification.Verified && verification.Outcome != AuthorizationRecoveryLedgerVerificationOutcome.Empty)
 throw new AuthorizationRecoveryLedgerPersistenceException(
 $"Refusing to append to an invalid ledger: {verification.Reason}");

 if (_records.Count == 0)
 {
 if (record.PreviousRecordDigest != AuthorizationRecoveryReconfigurationLedger.GenesisPreviousDigest)
 throw new AuthorizationRecoveryLedgerPersistenceException("The first record does not chain from genesis.");
 }
 else
 {
 var previous = _records[^1];
 if (record.ConfigVersion != previous.ConfigVersion + 1 ||
 !string.Equals(record.PreviousRecordDigest, previous.RecordDigest, StringComparison.OrdinalIgnoreCase))
 throw new AuthorizationRecoveryLedgerPersistenceException("The record does not extend the durable ledger head.");
 }

 if (_memberships.ContainsKey(record.ConfigVersion))
 throw new AuthorizationRecoveryLedgerPersistenceException("The configuration version already exists.");

 var ids = witnessIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
 var expectedDigest = AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(ids);
 if (!string.Equals(expectedDigest, record.MembershipDigest, StringComparison.OrdinalIgnoreCase))
 throw new AuthorizationRecoveryLedgerPersistenceException("The witness manifest does not match the record membership digest.");

 _records.Add(record);
 _memberships.Add(record.ConfigVersion, ids);
 return ValueTask.CompletedTask;
 }
 }
}

/// <summary>Raised when durable ledger persistence or reconciliation cannot be proven safe.</summary>
public sealed class AuthorizationRecoveryLedgerPersistenceException : Exception
{
 public AuthorizationRecoveryLedgerPersistenceException(string message) : base(message) { }
}
