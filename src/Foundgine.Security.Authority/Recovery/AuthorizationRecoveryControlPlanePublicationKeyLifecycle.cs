using System.Security.Cryptography;

namespace Foundgine.Security.Authority;

public enum AuthorizationRecoveryKeyStatus
{
    Active,
    VerificationOnly,
    Retired
}

public sealed record AuthorizationRecoveryIntegrityKey(
    string KeyId,
    AuthorizationRecoveryKeyStatus Status,
    int Version);

public sealed record AuthorizationRecoveryKeyRing(
    string ActiveKeyId,
    IReadOnlyDictionary<string, AuthorizationRecoveryIntegrityKey> Keys);

public enum AuthorizationRecoveryKeyLifecycleResult
{
    Activated,
    AlreadyActive,
    NotFound,
    CannotActivateRetiredKey,
    Retired,
    AlreadyRetired,
    CannotRetireActiveKey,
    VerificationAllowed,
    VerificationRejected,
    StaleRotation,
    ConflictingRotation
}

/// <summary>
/// Reference model for atomic integrity-key lifecycle transitions.
/// Key material is deliberately absent: production key material belongs to an
/// external secret/key-management system.
/// </summary>
public sealed class AuthorizationRecoveryControlPlanePublicationKeyLifecycle
{
    private readonly object _gate = new();
    private AuthorizationRecoveryKeyRing _ring;

    public AuthorizationRecoveryControlPlanePublicationKeyLifecycle(
        AuthorizationRecoveryKeyRing initialRing)
    {
        _ring = initialRing;
    }

    public AuthorizationRecoveryKeyRing Current
    {
        get { lock (_gate) return _ring; }
    }

    public AuthorizationRecoveryKeyLifecycleResult Rotate(
        string expectedActiveKeyId,
        string newKeyId,
        int newVersion)
    {
        lock (_gate)
        {
            if (!string.Equals(_ring.ActiveKeyId, expectedActiveKeyId, StringComparison.Ordinal))
                return AuthorizationRecoveryKeyLifecycleResult.StaleRotation;

            if (_ring.Keys.TryGetValue(newKeyId, out var existing))
            {
                if (existing.Status == AuthorizationRecoveryKeyStatus.Retired)
                    return AuthorizationRecoveryKeyLifecycleResult.CannotActivateRetiredKey;

                if (existing.Status == AuthorizationRecoveryKeyStatus.Active)
                    return AuthorizationRecoveryKeyLifecycleResult.AlreadyActive;

                if (existing.Version != newVersion)
                    return AuthorizationRecoveryKeyLifecycleResult.ConflictingRotation;
            }

            var next = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(_ring.Keys, StringComparer.Ordinal)
            {
                [expectedActiveKeyId] = new(
                    expectedActiveKeyId,
                    AuthorizationRecoveryKeyStatus.VerificationOnly,
                    _ring.Keys[expectedActiveKeyId].Version),

                [newKeyId] = new(
                    newKeyId,
                    AuthorizationRecoveryKeyStatus.Active,
                    newVersion)
            };

            _ring = new AuthorizationRecoveryKeyRing(newKeyId, next);
            return AuthorizationRecoveryKeyLifecycleResult.Activated;
        }
    }

    public AuthorizationRecoveryKeyLifecycleResult Retire(
        string keyId,
        string expectedActiveKeyId)
    {
        lock (_gate)
        {
            if (!_ring.Keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoveryKeyLifecycleResult.NotFound;

            if (key.Status == AuthorizationRecoveryKeyStatus.Retired)
                return AuthorizationRecoveryKeyLifecycleResult.AlreadyRetired;

            if (string.Equals(keyId, expectedActiveKeyId, StringComparison.Ordinal) &&
                string.Equals(_ring.ActiveKeyId, keyId, StringComparison.Ordinal))
                return AuthorizationRecoveryKeyLifecycleResult.CannotRetireActiveKey;

            var next = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(_ring.Keys, StringComparer.Ordinal)
            {
                [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
            };

            _ring = new AuthorizationRecoveryKeyRing(_ring.ActiveKeyId, next);
            return AuthorizationRecoveryKeyLifecycleResult.Retired;
        }
    }

    public AuthorizationRecoveryKeyLifecycleResult CheckVerification(string keyId)
    {
        lock (_gate)
        {
            if (!_ring.Keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoveryKeyLifecycleResult.NotFound;

            return key.Status == AuthorizationRecoveryKeyStatus.Retired
                ? AuthorizationRecoveryKeyLifecycleResult.VerificationRejected
                : AuthorizationRecoveryKeyLifecycleResult.VerificationAllowed;
        }
    }
}
