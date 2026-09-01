using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Security.Authority;

/// <summary>
/// source-authenticated replication envelope for the witness credential
/// lifecycle journal. Secret material is never carried in the envelope; the
/// source key only proves that an authorized replication source published the
/// exact lifecycle record.
/// </summary>
public sealed record AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope(
 AuthorizationRecoveryWitnessCredentialLifecycleRecord Record,
 string SourceInstanceId,
 string SourceKeyId,
 string IntegrityProof);

public enum AuthorizationRecoveryWitnessSourceTrustKeyStatus
{
 Active,
 VerificationOnly,
 Revoked
}

public sealed record AuthorizationRecoveryWitnessSourceTrustKey(
 string KeyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus Status,
 int Version,
 byte[] KeyMaterial);

public enum AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult
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

public enum AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult
{
 Applied,
 AlreadyApplied,
 InvalidIntegrity,
 UntrustedSource,
 UnknownSourceKey,
 RevokedSourceKey,
 DivergentSource,
 InvalidRecord,
 InvalidHistory,
 Gap,
 PreviousDigestMismatch,
 DivergentRevision,
 StaleRecovery
}

/// <summary>
/// source-authentication boundary. A lifecycle record is accepted only
/// after the publisher identity and source credential have authenticated the
/// exact record. The underlying journal still enforces contiguous
/// revisions, previous-digest chaining, duplicate idempotency and fork safety.
/// </summary>
public sealed class AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication
{
 private readonly object _gate = new();
 private readonly string _instanceId;
 private string _localSourceKeyId = "witness-source-key-v1";
 private int _localSourceKeyVersion = 1;
 private byte[] _localSourceKey;
 private readonly Dictionary<string, Dictionary<string, AuthorizationRecoveryWitnessSourceTrustKey>> _trustedSourceKeys =
 new(StringComparer.Ordinal);
 private readonly AuthorizationRecoveryWitnessCredentialLifecycleReplication _journal =
 new();

 public AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication(
 string instanceId,
 ReadOnlySpan<byte> sourceKey)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
 ValidateKey(sourceKey, nameof(sourceKey));

 _instanceId = instanceId;
 _localSourceKey = sourceKey.ToArray();
 _trustedSourceKeys[_instanceId] = new(StringComparer.Ordinal)
 {
 [_localSourceKeyId] = new(
 _localSourceKeyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active,
 _localSourceKeyVersion,
 _localSourceKey.ToArray())
 };
 }

 public string InstanceId => _instanceId;

 public string LocalSourceKeyId
 {
 get { lock (_gate) return _localSourceKeyId; }
 }

 public long Revision => _journal.Revision;
 public string HeadDigest => _journal.HeadDigest;

 public IReadOnlyList<AuthorizationRecoveryWitnessCredentialLifecycleRecord> ReadAll() => _journal.ReadAll();

 public void TrustSourceInstance(string sourceInstanceId, ReadOnlySpan<byte> sourceKey) =>
 TrustSourceKey(sourceInstanceId, "witness-source-key-v1", 1, sourceKey);

 public void TrustSourceKey(
 string sourceInstanceId,
 string keyId,
 int version,
 ReadOnlySpan<byte> sourceKey)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(sourceInstanceId);
 ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
 if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
 ValidateKey(sourceKey, nameof(sourceKey));

 lock (_gate)
 {
 if (string.Equals(sourceInstanceId, _instanceId, StringComparison.Ordinal))
 throw new InvalidOperationException("The local instance cannot be registered as a remote source.");

 if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys))
 _trustedSourceKeys[sourceInstanceId] = keys = new(StringComparer.Ordinal);

 keys[keyId] = new(
 keyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active,
 version,
 sourceKey.ToArray());
 }
 }

 public AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult RotateTrustedSourceKey(
 string sourceInstanceId,
 string expectedActiveKeyId,
 string newKeyId,
 int newVersion,
 ReadOnlySpan<byte> newKeyMaterial)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(sourceInstanceId);
 ArgumentException.ThrowIfNullOrWhiteSpace(expectedActiveKeyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(newKeyId);
 if (newVersion < 1) throw new ArgumentOutOfRangeException(nameof(newVersion));
 ValidateKey(newKeyMaterial, nameof(newKeyMaterial));

 lock (_gate)
 {
 if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys) ||
 !keys.TryGetValue(expectedActiveKeyId, out var current))
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.NotFound;

 if (current.Status != AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.StaleRotation;

 if (keys.TryGetValue(newKeyId, out var existing))
 {
 if (existing.Status == AuthorizationRecoveryWitnessSourceTrustKeyStatus.Revoked)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.CannotActivateRevokedKey;
 if (existing.Status == AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.AlreadyActive;
 if (existing.Version != newVersion)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.ConflictingRotation;
 }

 keys[expectedActiveKeyId] = current with
 {
 Status = AuthorizationRecoveryWitnessSourceTrustKeyStatus.VerificationOnly
 };
 keys[newKeyId] = new(
 newKeyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active,
 newVersion,
 newKeyMaterial.ToArray());
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Activated;
 }
 }

 public AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult RevokeTrustedSourceKey(
 string sourceInstanceId,
 string keyId)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(sourceInstanceId);
 ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

 lock (_gate)
 {
 if (!_trustedSourceKeys.TryGetValue(sourceInstanceId, out var keys) ||
 !keys.TryGetValue(keyId, out var key))
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.NotFound;

 if (key.Status == AuthorizationRecoveryWitnessSourceTrustKeyStatus.Revoked)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.AlreadyRevoked;
 if (key.Status == AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active)
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.CannotRevokeActiveKey;

 keys[keyId] = key with { Status = AuthorizationRecoveryWitnessSourceTrustKeyStatus.Revoked };
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Revoked;
 }
 }

 public AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult RotateLocalSourceKey(
 string expectedActiveKeyId,
 string newKeyId,
 int newVersion,
 ReadOnlySpan<byte> newKeyMaterial)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(expectedActiveKeyId);
 ArgumentException.ThrowIfNullOrWhiteSpace(newKeyId);
 if (newVersion < 1) throw new ArgumentOutOfRangeException(nameof(newVersion));
 ValidateKey(newKeyMaterial, nameof(newKeyMaterial));

 lock (_gate)
 {
 if (!string.Equals(_localSourceKeyId, expectedActiveKeyId, StringComparison.Ordinal))
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.StaleRotation;

 if (_trustedSourceKeys[_instanceId].ContainsKey(newKeyId))
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.AlreadyActive;

 _trustedSourceKeys[_instanceId][_localSourceKeyId] =
 new(_localSourceKeyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus.VerificationOnly,
 _localSourceKeyVersion,
 _localSourceKey.ToArray());

 _localSourceKey = newKeyMaterial.ToArray();
 _localSourceKeyId = newKeyId;
 _localSourceKeyVersion = newVersion;
 _trustedSourceKeys[_instanceId][newKeyId] =
 new(newKeyId,
 AuthorizationRecoveryWitnessSourceTrustKeyStatus.Active,
 newVersion,
 newKeyMaterial.ToArray());
 return AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Activated;
 }
 }

 public AuthorizationRecoveryWitnessCredentialLifecycleRecord AppendAndAuthenticate(
 string witnessId,
 string credentialFingerprint,
 long credentialSequence,
 AuthorizationRecoveryWitnessCredentialState state)
 {
 lock (_gate)
 {
 var record = _journal.Append(witnessId, credentialFingerprint, credentialSequence, state);
 return record;
 }
 }

 public AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope CreateEnvelope(
 AuthorizationRecoveryWitnessCredentialLifecycleRecord record)
 {
 ArgumentNullException.ThrowIfNull(record);
 lock (_gate)
 {
 var key = _trustedSourceKeys[_instanceId][_localSourceKeyId];
 var envelope = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope(
 record,
 _instanceId,
 _localSourceKeyId,
 string.Empty);
 return envelope with { IntegrityProof = ComputeIntegrityProof(envelope, key.KeyMaterial) };
 }
 }

 public AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope AppendAndCreateEnvelope(
 string witnessId,
 string credentialFingerprint,
 long credentialSequence,
 AuthorizationRecoveryWitnessCredentialState state)
 {
 return CreateEnvelope(AppendAndAuthenticate(
 witnessId, credentialFingerprint, credentialSequence, state));
 }

 public AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult Apply(
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope envelope)
 {
 ArgumentNullException.ThrowIfNull(envelope);
 lock (_gate)
 {
 var authenticationResult = AuthenticateEnvelope(envelope, out var sourceKey);
 if (authenticationResult != AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied)
 return authenticationResult;

 if (string.Equals(envelope.SourceInstanceId, _instanceId, StringComparison.Ordinal))
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.DivergentSource;

 var result = _journal.Apply(envelope.Record);
 return Map(result);
 }
 }

 public AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult Recover(
 IReadOnlyList<AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope> envelopes,
 string headDigest)
 {
 if (envelopes is null || headDigest is null)
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidHistory;

 lock (_gate)
 {
 // Authenticate the complete package before mutating the local journal.
 // This prevents a package from being partially accepted before a later
 // envelope proves to be forged or published by an unauthorized source.
 foreach (var envelope in envelopes)
 {
 var authenticationResult = AuthenticateEnvelope(envelope, out _);
 if (authenticationResult != AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied)
 return authenticationResult;
 }

 var package = new AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage(
 envelopes.Select(static e => e.Record).ToArray(),
 headDigest);
 return Map(_journal.Recover(package));
 }
 }

 public IReadOnlyList<AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope> ExportAuthenticatedHistory()
 {
 lock (_gate)
 {
 return _journal.ReadAll().Select(CreateEnvelope).ToArray();
 }
 }

 private AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult AuthenticateEnvelope(
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope envelope,
 out AuthorizationRecoveryWitnessSourceTrustKey? sourceKey)
 {
 sourceKey = null;
 if (!_trustedSourceKeys.TryGetValue(envelope.SourceInstanceId, out var keys))
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.UntrustedSource;
 if (!keys.TryGetValue(envelope.SourceKeyId, out sourceKey))
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.UnknownSourceKey;
 if (sourceKey.Status == AuthorizationRecoveryWitnessSourceTrustKeyStatus.Revoked)
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.RevokedSourceKey;

 var expected = ComputeIntegrityProof(envelope, sourceKey.KeyMaterial);
 if (!FixedEquals(expected, envelope.IntegrityProof))
 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidIntegrity;

 return AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied;
 }

 private static AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult Map(
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult result) => result switch
 {
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.AlreadyApplied => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.AlreadyApplied,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidRecord => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidRecord,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Gap => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Gap,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.PreviousDigestMismatch => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.PreviousDigestMismatch,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.DivergentRevision => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.DivergentRevision,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidHistory => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidHistory,
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.StaleRecovery => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.StaleRecovery,
 _ => AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidHistory
 };

 public static string ComputeIntegrityProof(
 AuthorizationRecoveryWitnessCredentialLifecycleReplicationEnvelope envelope,
 ReadOnlySpan<byte> sourceKey)
 {
 ValidateKey(sourceKey, nameof(sourceKey));
 var record = envelope.Record;
 var canonical = string.Join("|",
 record.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
 record.WitnessId,
 record.CredentialFingerprint,
 record.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
 record.State.ToString(),
 record.PreviousDigest,
 record.Digest,
 envelope.SourceInstanceId,
 envelope.SourceKeyId);

 using var hmac = new HMACSHA256(sourceKey.ToArray());
 return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
 }

 private static void ValidateKey(ReadOnlySpan<byte> key, string parameterName)
 {
 if (key.Length < 16)
 throw new ArgumentException("Source key must be at least 128 bits.", parameterName);
 }

 private static bool FixedEquals(string left, string right) =>
 CryptographicOperations.FixedTimeEquals(
 Encoding.UTF8.GetBytes(left ?? string.Empty),
 Encoding.UTF8.GetBytes(right ?? string.Empty));
}
