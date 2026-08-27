namespace Foundgine.Security.Authority;

public enum AuthorizationRecoveryRetirementRecoveryResult
{
    Recovered,
    RejectedRetiredKey,
    VerificationFailed,
    StaleRecovery,
    PromotionVerified,
    PromotionRejected,
    Retired,
    HistoricalPublicationStillProtected,
    StaleRetirement,
    ConcurrentRetirementLost
}

/// <summary>
/// Reference model for the retirement/recovery concurrency boundary.
/// Verification of a historical publication and retirement of its generation
/// share one linearization point. A recovery operation therefore observes
/// either the verification-only generation before retirement or the retired
/// generation after retirement; it cannot verify against a generation that is
/// simultaneously being retired.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneKeyRetirementRecoveryConcurrency
{
    private readonly object _gate = new();
    private readonly Func<string, byte[]?> _keyResolver;
    private AuthorizationRecoveryKeyRing _ring;
    private AuthorizationRecoveryControlPlanePublication _authoritativePublication;
    private readonly long _verificationWindowSequences;
    private long _currentSequence;
    private readonly Dictionary<string, long> _recoveredHistoricalSequenceByKey = new(StringComparer.Ordinal);

    public AuthorizationRecoveryControlPlaneKeyRetirementRecoveryConcurrency(
        AuthorizationRecoveryKeyRing initialRing,
        AuthorizationRecoveryControlPlanePublication authoritativePublication,
        long verificationWindowSequences,
        Func<string, byte[]?> keyResolver)
    {
        ArgumentNullException.ThrowIfNull(initialRing);
        ArgumentNullException.ThrowIfNull(authoritativePublication);
        ArgumentNullException.ThrowIfNull(keyResolver);

        if (verificationWindowSequences < 0)
            throw new ArgumentOutOfRangeException(nameof(verificationWindowSequences));

        if (!initialRing.Keys.TryGetValue(initialRing.ActiveKeyId, out var active) ||
            active.Status != AuthorizationRecoveryKeyStatus.Active)
        {
            throw new ArgumentException("Initial ring must contain an active key.", nameof(initialRing));
        }

        if (!string.Equals(
                authoritativePublication.IntegrityKeyId,
                initialRing.ActiveKeyId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Authoritative publication must reference the active generation.",
                nameof(authoritativePublication));
        }

        _ring = initialRing;
        _authoritativePublication = authoritativePublication;
        _currentSequence = authoritativePublication.Sequence;
        _verificationWindowSequences = verificationWindowSequences;
        _keyResolver = keyResolver;
    }

    public AuthorizationRecoveryKeyRing CurrentRing
    {
        get { lock (_gate) return _ring; }
    }

    public AuthorizationRecoveryControlPlanePublication CurrentPublication
    {
        get { lock (_gate) return _authoritativePublication; }
    }

    public long CurrentSequence
    {
        get { lock (_gate) return _currentSequence; }
    }

    /// <summary>
    /// Records a newer authoritative publication without changing the active
    /// generation. This is the advancement that can close a historical
    /// verification window.
    /// </summary>
    public void RecordPublication(AuthorizationRecoveryControlPlanePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        lock (_gate)
        {
            if (publication.Sequence < _currentSequence)
                throw new InvalidOperationException("Publication sequence cannot move backwards.");

            if (!_ring.Keys.TryGetValue(publication.IntegrityKeyId, out var key) ||
                key.Status == AuthorizationRecoveryKeyStatus.Retired)
            {
                throw new InvalidOperationException(
                    "A publication cannot reference a missing or retired key generation.");
            }

            _currentSequence = publication.Sequence;
            _authoritativePublication = publication;
        }
    }

    /// <summary>
    /// Recovery verification and promotion are one atomic operation. A
    /// retirement that wins the lock first causes recovery to fail closed;
    /// recovery that wins first completes against the still-verification-only
    /// generation before retirement can commit.
    /// </summary>
    public AuthorizationRecoveryRetirementRecoveryResult TryRecoverAndPromote(
        AuthorizationRecoveryControlPlanePublication candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        lock (_gate)
        {
            if (!SamePublication(candidate, _authoritativePublication))
                return AuthorizationRecoveryRetirementRecoveryResult.PromotionRejected;

            if (!_ring.Keys.TryGetValue(candidate.IntegrityKeyId, out var key) ||
                key.Status == AuthorizationRecoveryKeyStatus.Retired)
            {
                return AuthorizationRecoveryRetirementRecoveryResult.RejectedRetiredKey;
            }

            if (!VerifyUnderLock(candidate))
                return AuthorizationRecoveryRetirementRecoveryResult.VerificationFailed;

            return AuthorizationRecoveryRetirementRecoveryResult.PromotionVerified;
        }
    }

    /// <summary>
    /// Historical recovery can verify a publication from a previous
    /// generation only while that generation remains VerificationOnly.
    /// </summary>
    public AuthorizationRecoveryRetirementRecoveryResult TryRecoverHistorical(
        AuthorizationRecoveryControlPlanePublication historicalPublication)
    {
        ArgumentNullException.ThrowIfNull(historicalPublication);

        lock (_gate)
        {
            if (!_ring.Keys.TryGetValue(historicalPublication.IntegrityKeyId, out var key) ||
                key.Status == AuthorizationRecoveryKeyStatus.Retired)
            {
                return AuthorizationRecoveryRetirementRecoveryResult.RejectedRetiredKey;
            }

            // The verification window gates retirement of the signing generation
            // (see TryRetire), not eligibility to recover a historical publication.
            if (!VerifyUnderLock(historicalPublication))
                return AuthorizationRecoveryRetirementRecoveryResult.VerificationFailed;

            if (!_recoveredHistoricalSequenceByKey.TryGetValue(
                    historicalPublication.IntegrityKeyId, out var trackedSequence) ||
                historicalPublication.Sequence > trackedSequence)
            {
                _recoveredHistoricalSequenceByKey[historicalPublication.IntegrityKeyId] =
                    historicalPublication.Sequence;
            }

            return AuthorizationRecoveryRetirementRecoveryResult.Recovered;
        }
    }

    /// <summary>
    /// Retirement and the historical-window check are serialized with
    /// recovery verification. This prevents a time-of-check/time-of-use gap
    /// in which recovery verifies a key after the generation has been retired.
    /// </summary>
    public AuthorizationRecoveryRetirementRecoveryResult TryRetire(
        string keyId,
        string expectedActiveKeyId,
        long expectedCurrentSequence)
    {
        lock (_gate)
        {
            if (!string.Equals(_ring.ActiveKeyId, expectedActiveKeyId, StringComparison.Ordinal) ||
                _currentSequence != expectedCurrentSequence)
            {
                return AuthorizationRecoveryRetirementRecoveryResult.StaleRetirement;
            }

            if (!_ring.Keys.TryGetValue(keyId, out var key))
                return AuthorizationRecoveryRetirementRecoveryResult.PromotionRejected;

            if (key.Status == AuthorizationRecoveryKeyStatus.Retired)
                return AuthorizationRecoveryRetirementRecoveryResult.ConcurrentRetirementLost;

            if (string.Equals(keyId, _ring.ActiveKeyId, StringComparison.Ordinal))
                return AuthorizationRecoveryRetirementRecoveryResult.PromotionRejected;

            if (key.Status != AuthorizationRecoveryKeyStatus.VerificationOnly)
                return AuthorizationRecoveryRetirementRecoveryResult.PromotionRejected;

            var protectedFrom = _currentSequence - _verificationWindowSequences;
            var currentPublicationProtected = _authoritativePublication.Sequence >= protectedFrom &&
                string.Equals(
                    _authoritativePublication.IntegrityKeyId,
                    keyId,
                    StringComparison.Ordinal);
            var recoveredHistoricalProtected =
                _recoveredHistoricalSequenceByKey.TryGetValue(keyId, out var recoveredSequence) &&
                recoveredSequence >= protectedFrom;

            if (currentPublicationProtected || recoveredHistoricalProtected)
                return AuthorizationRecoveryRetirementRecoveryResult.HistoricalPublicationStillProtected;

            var nextKeys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(
                _ring.Keys,
                StringComparer.Ordinal)
            {
                [keyId] = key with { Status = AuthorizationRecoveryKeyStatus.Retired }
            };

            _ring = new AuthorizationRecoveryKeyRing(_ring.ActiveKeyId, nextKeys);
            return AuthorizationRecoveryRetirementRecoveryResult.Retired;
        }
    }

    private bool VerifyUnderLock(AuthorizationRecoveryControlPlanePublication publication)
    {
        var material = _keyResolver(publication.IntegrityKeyId);
        return material is not null &&
            AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(publication, material);
    }

    private static bool SamePublication(
        AuthorizationRecoveryControlPlanePublication left,
        AuthorizationRecoveryControlPlanePublication right)
    {
        return left.Epoch == right.Epoch &&
               left.Sequence == right.Sequence &&
               string.Equals(left.ActiveControlPlaneId, right.ActiveControlPlaneId, StringComparison.Ordinal) &&
               string.Equals(left.HeadDigest, right.HeadDigest, StringComparison.Ordinal) &&
               string.Equals(left.IntegrityKeyId, right.IntegrityKeyId, StringComparison.Ordinal) &&
               string.Equals(left.AlgorithmVersion, right.AlgorithmVersion, StringComparison.Ordinal) &&
               string.Equals(left.Tag, right.Tag, StringComparison.Ordinal);
    }
}
