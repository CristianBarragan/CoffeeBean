namespace Foundgine.Runtime.ControlPlane;

public enum AuthorizationRecoveryKeyRetirementResult
{
    Retired,
    AlreadyRetired,
    VerificationAllowed,
    VerificationRejected,
    NotFound,
    CannotRetireActiveKey,
    VerificationWindowOpen,
    HistoricalPublicationStillProtected,
    StaleRetirement,
    ConcurrentRetirementLost
}

/// <summary>
/// Reference model for the VerificationOnly -> Retired boundary.
///
/// Retirement is not merely a metadata transition. The control plane tracks
/// publications that were authenticated by each generation and refuses to
/// retire a non-active generation while a publication signed by it is still
/// inside the configured historical verification window.
/// </summary>
public sealed class AuthorizationRecoveryControlPlanePublicationKeyRetirement
{
    private readonly object _gate = new();
    private readonly long _verificationWindowSequences;
    private AuthorizationRecoveryKeyRing _ring;
    private readonly List<AuthorizationRecoveryControlPlanePublication> _publications = new();
    private long _currentSequence;

    public AuthorizationRecoveryControlPlanePublicationKeyRetirement(
        AuthorizationRecoveryKeyRing initialRing,
        long verificationWindowSequences)
    {
        ArgumentNullException.ThrowIfNull(initialRing);
        if (verificationWindowSequences < 0)
            throw new ArgumentOutOfRangeException(nameof(verificationWindowSequences));

        if (!initialRing.Keys.TryGetValue(initialRing.ActiveKeyId, out var active) ||
            active.Status != AuthorizationRecoveryKeyStatus.Active)
        {
            throw new ArgumentException("Initial ring must contain an active key.", nameof(initialRing));
        }

        _ring = initialRing;
        _verificationWindowSequences = verificationWindowSequences;
    }

    public AuthorizationRecoveryKeyRing Current
    {
        get { lock (_gate) return _ring; }
    }

    public long CurrentSequence
    {
        get { lock (_gate) return _currentSequence; }
    }

    public void RecordAuthoritativePublication(
        AuthorizationRecoveryControlPlanePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        lock (_gate)
        {
            if (!_ring.Keys.TryGetValue(publication.IntegrityKeyId, out var key) ||
                key.Status == AuthorizationRecoveryKeyStatus.Retired)
            {
                throw new InvalidOperationException(
                    "A publication cannot be recorded against a missing or retired key generation.");
            }

            if (publication.Sequence < _currentSequence)
                throw new InvalidOperationException("Publication sequence cannot move backwards.");

            _currentSequence = publication.Sequence;
            _publications.Add(publication);
        }
    }

    /// <summary>
    /// Atomically closes the verification window and retires a generation.
    /// A stale caller loses if the active generation or sequence changed since
    /// the caller observed the state.
    /// </summary>
    public AuthorizationRecoveryKeyRetirementResult TryRetire(
        string keyId,
        string expectedActiveKeyId,
        long expectedCurrentSequence)
    {
        lock (_gate)
        {
            if (!string.Equals(_ring.ActiveKeyId, expectedActiveKeyId, StringComparison.Ordinal) ||
                _currentSequence != expectedCurrentSequence)
            {
                return AuthorizationRecoveryKeyRetirementResult.StaleRetirement;
            }

            if (!_ring.Keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoveryKeyRetirementResult.NotFound;

            if (key.Status == AuthorizationRecoveryKeyStatus.Retired)
                return AuthorizationRecoveryKeyRetirementResult.AlreadyRetired;

            if (string.Equals(keyId, _ring.ActiveKeyId, StringComparison.Ordinal) ||
                string.Equals(keyId, expectedActiveKeyId, StringComparison.Ordinal))
            {
                return AuthorizationRecoveryKeyRetirementResult.CannotRetireActiveKey;
            }

            if (key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly)
                return AuthorizationRecoveryKeyRetirementResult.CannotRetireActiveKey;

            var protectedFrom = _currentSequence - _verificationWindowSequences;
            var protectedPublicationExists = _publications.Any(p =>
                string.Equals(p.IntegrityKeyId, keyId, StringComparison.Ordinal) &&
                p.Sequence >= protectedFrom);

            if (protectedPublicationExists)
                return AuthorizationRecoveryKeyRetirementResult.HistoricalPublicationStillProtected;

            var next = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(
                _ring.Keys,
                StringComparer.Ordinal)
            {
                [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
            };

            _ring = new AuthorizationRecoveryKeyRing(_ring.ActiveKeyId, next);
            return AuthorizationRecoveryKeyRetirementResult.Retired;
        }
    }

    public AuthorizationRecoveryKeyRetirementResult CheckVerification(string keyId)
    {
        lock (_gate)
        {
            if (!_ring.Keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoveryKeyRetirementResult.NotFound;

            return key.Status == AuthorizationRecoveryKeyStatus.Retired
                ? AuthorizationRecoveryKeyRetirementResult.VerificationRejected
                : AuthorizationRecoveryKeyRetirementResult.VerificationAllowed;
        }
    }
}
