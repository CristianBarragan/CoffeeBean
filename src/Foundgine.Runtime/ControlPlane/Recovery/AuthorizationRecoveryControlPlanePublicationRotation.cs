namespace Foundgine.Runtime.ControlPlane;

/// <summary>
/// Atomically couples publication state with the authorization-recovery
/// integrity-key generation that authenticates it.
/// </summary>
public sealed record AuthorizationRecoveryControlPlanePublicationRotationState(
    AuthorizationRecoveryKeyRing KeyRing,
    AuthorizationRecoveryControlPlanePublication Publication);

public enum AuthorizationRecoveryPublicationRotationResult
{
    RotatedAndPublished,
    StaleRotation,
    CannotActivateRetiredKey,
    ConflictingRotation,
    SigningKeyUnavailable,
    PublicationRejected,
    InvalidSuccessorVersion,
    StalePublication,
    VerificationAllowed,
    VerificationRejected,
    PromotionVerified,
    PromotionRejected,
    StalePublicationWrite
}

/// <summary>
/// Reference model for the key-rotation/publication boundary.
///
/// The critical invariant is linearizability: a rotation and the first
/// publication signed by the successor key are one state transition. Readers,
/// recovery and promotion observe either the old coherent pair or the new
/// coherent pair; they cannot observe an active key whose publication is still
/// signed by an incomplete successor generation.
///
/// Key material is resolved externally and is never stored in the control-plane
/// metadata model.
/// </summary>
public sealed class AuthorizationRecoveryControlPlanePublicationRotation
{
    private readonly object _gate = new();
    private readonly Func<string, byte[]?> _keyResolver;
    private AuthorizationRecoveryControlPlanePublicationRotationState _state;

    public AuthorizationRecoveryControlPlanePublicationRotation(
        AuthorizationRecoveryControlPlanePublicationRotationState initialState,
        Func<string, byte[]?> keyResolver)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(keyResolver);

        ValidateInitialState(initialState);
        _state = initialState;
        _keyResolver = keyResolver;
    }

    public AuthorizationRecoveryControlPlanePublicationRotationState Current
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>
    /// Writes a publication using exactly the active generation observed by
    /// the caller. The compare-and-publish is atomic with rotation, so a stale
    /// writer cannot publish new state under an older generation after the
    /// successor becomes active.
    /// </summary>
    public AuthorizationRecoveryPublicationRotationResult TryPublish(
        string expectedActiveKeyId,
        long epoch,
        string activeControlPlaneId,
        long sequence,
        string headDigest)
    {
        lock (_gate)
        {
            if (!string.Equals(
                    _state.KeyRing.ActiveKeyId,
                    expectedActiveKeyId,
                    StringComparison.Ordinal))
            {
                return AuthorizationRecoveryPublicationRotationResult.StalePublicationWrite;
            }

            if (!_state.KeyRing.Keys.TryGetValue(expectedActiveKeyId, out var key) ||
                key.Status != AuthorizationRecoveryKeyStatus.Active)
            {
                return AuthorizationRecoveryPublicationRotationResult.PublicationRejected;
            }

            if (epoch < _state.Publication.Epoch ||
                sequence < _state.Publication.Sequence)
            {
                return AuthorizationRecoveryPublicationRotationResult.StalePublication;
            }

            var material = _keyResolver(expectedActiveKeyId);
            if (material is null)
                return AuthorizationRecoveryPublicationRotationResult.SigningKeyUnavailable;

            var tag = AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
                epoch,
                activeControlPlaneId,
                sequence,
                headDigest,
                expectedActiveKeyId,
                material);

            _state = _state with
            {
                Publication = new AuthorizationRecoveryControlPlanePublication(
                    epoch,
                    activeControlPlaneId,
                    sequence,
                    headDigest,
                    expectedActiveKeyId,
                    AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
                    tag)
            };

            return AuthorizationRecoveryPublicationRotationResult.RotatedAndPublished;
        }
    }

    /// <summary>
    /// Atomically activates the successor key and publishes the first
    /// publication authenticated by that generation.
    /// </summary>
    public AuthorizationRecoveryPublicationRotationResult TryRotateAndPublish(
        string expectedActiveKeyId,
        string newKeyId,
        int newVersion,
        long epoch,
        string activeControlPlaneId,
        long sequence,
        string headDigest)
    {
        lock (_gate)
        {
            if (!string.Equals(
                    _state.KeyRing.ActiveKeyId,
                    expectedActiveKeyId,
                    StringComparison.Ordinal))
            {
                return AuthorizationRecoveryPublicationRotationResult.StaleRotation;
            }

            if (!_state.KeyRing.Keys.TryGetValue(expectedActiveKeyId, out var currentActive) ||
                currentActive.Status != AuthorizationRecoveryKeyStatus.Active)
            {
                return AuthorizationRecoveryPublicationRotationResult.StaleRotation;
            }

            if (newVersion <= currentActive.Version)
                return AuthorizationRecoveryPublicationRotationResult.InvalidSuccessorVersion;

            if (_state.KeyRing.Keys.TryGetValue(newKeyId, out var existing))
            {
                if (existing.Status == AuthorizationRecoveryKeyStatus.Retired)
                    return AuthorizationRecoveryPublicationRotationResult.CannotActivateRetiredKey;

                if (existing.Status == AuthorizationRecoveryKeyStatus.Active)
                    return AuthorizationRecoveryPublicationRotationResult.ConflictingRotation;

                if (existing.Version != newVersion)
                    return AuthorizationRecoveryPublicationRotationResult.ConflictingRotation;
            }

            if (epoch < _state.Publication.Epoch ||
                sequence < _state.Publication.Sequence)
            {
                return AuthorizationRecoveryPublicationRotationResult.StalePublication;
            }

            // The successor cannot become active unless its external key
            // material is already resolvable. This closes the incomplete-key
            // activation window.
            var material = _keyResolver(newKeyId);
            if (material is null)
                return AuthorizationRecoveryPublicationRotationResult.SigningKeyUnavailable;

            var tag = AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
                epoch,
                activeControlPlaneId,
                sequence,
                headDigest,
                newKeyId,
                material);

            var nextKeys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>(
                _state.KeyRing.Keys,
                StringComparer.Ordinal)
            {
                [expectedActiveKeyId] = currentActive with
                {
                    Status = AuthorizationRecoveryKeyStatus.VerificationOnly
                },
                [newKeyId] = new(
                    newKeyId,
                    AuthorizationRecoveryKeyStatus.Active,
                    newVersion)
            };

            var nextRing = new AuthorizationRecoveryKeyRing(newKeyId, nextKeys);
            var nextPublication = new AuthorizationRecoveryControlPlanePublication(
                epoch,
                activeControlPlaneId,
                sequence,
                headDigest,
                newKeyId,
                AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
                tag);

            // Single assignment is the publication/key lifecycle commit point.
            _state = new AuthorizationRecoveryControlPlanePublicationRotationState(
                nextRing,
                nextPublication);

            return AuthorizationRecoveryPublicationRotationResult.RotatedAndPublished;
        }
    }

    /// <summary>
    /// Verifies the authoritative publication against the key generation
    /// named by the publication. Verification-only keys remain valid; retired
    /// keys are rejected.
    /// </summary>
    public AuthorizationRecoveryPublicationRotationResult VerifyCurrentPublication()
    {
        lock (_gate)
        {
            return VerifyPublicationUnderLock(_state.Publication);
        }
    }

    /// <summary>
    /// Verifies an existing publication against the lifecycle state currently
    /// governing its key generation. This is the continuity check used by
    /// recovery readers for historical publications.
    /// </summary>
    public AuthorizationRecoveryPublicationRotationResult VerifyPublication(
        AuthorizationRecoveryControlPlanePublication publication)
    {
        lock (_gate)
        {
            return VerifyPublicationUnderLock(publication);
        }
    }

    /// <summary>
    /// Promotion is permitted only from the same authenticated publication
    /// that is currently authoritative. Verification and promotion share the
    /// same linearization boundary.
    /// </summary>
    public AuthorizationRecoveryPublicationRotationResult TryPromote(
        AuthorizationRecoveryControlPlanePublication candidate)
    {
        lock (_gate)
        {
            if (!string.Equals(
                    candidate.IntegrityKeyId,
                    _state.Publication.IntegrityKeyId,
                    StringComparison.Ordinal) ||
                candidate.Epoch != _state.Publication.Epoch ||
                candidate.Sequence != _state.Publication.Sequence ||
                !string.Equals(
                    candidate.HeadDigest,
                    _state.Publication.HeadDigest,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.ActiveControlPlaneId,
                    _state.Publication.ActiveControlPlaneId,
                    StringComparison.Ordinal))
            {
                return AuthorizationRecoveryPublicationRotationResult.PromotionRejected;
            }

            if (!_state.KeyRing.Keys.TryGetValue(
                    candidate.IntegrityKeyId,
                    out var key) ||
                key.Status == AuthorizationRecoveryKeyStatus.Retired)
            {
                return AuthorizationRecoveryPublicationRotationResult.PromotionRejected;
            }

            var material = _keyResolver(candidate.IntegrityKeyId);
            if (material is null ||
                !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
                    candidate,
                    material))
            {
                return AuthorizationRecoveryPublicationRotationResult.PromotionRejected;
            }

            return AuthorizationRecoveryPublicationRotationResult.PromotionVerified;
        }
    }

    private AuthorizationRecoveryPublicationRotationResult VerifyPublicationUnderLock(
        AuthorizationRecoveryControlPlanePublication publication)
    {
        if (!_state.KeyRing.Keys.TryGetValue(
                publication.IntegrityKeyId,
                out var key) ||
            key.Status == AuthorizationRecoveryKeyStatus.Retired)
        {
            return AuthorizationRecoveryPublicationRotationResult.VerificationRejected;
        }

        var material = _keyResolver(publication.IntegrityKeyId);
        if (material is null ||
            !AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
                publication,
                material))
        {
            return AuthorizationRecoveryPublicationRotationResult.VerificationRejected;
        }

        return AuthorizationRecoveryPublicationRotationResult.VerificationAllowed;
    }

    private static void ValidateInitialState(
        AuthorizationRecoveryControlPlanePublicationRotationState state)
    {
        if (string.IsNullOrWhiteSpace(state.KeyRing.ActiveKeyId))
            throw new ArgumentException("Active key ID is required.", nameof(state));

        if (!state.KeyRing.Keys.TryGetValue(
                state.KeyRing.ActiveKeyId,
                out var active) ||
            active.Status != AuthorizationRecoveryKeyStatus.Active)
        {
            throw new ArgumentException(
                "Initial ring must contain its active key in Active status.",
                nameof(state));
        }

        if (!string.Equals(
                state.Publication.IntegrityKeyId,
                state.KeyRing.ActiveKeyId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Initial publication must reference the active key generation.",
                nameof(state));
        }
    }
}
