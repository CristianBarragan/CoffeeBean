namespace Foundgine.Security.Authority;

/// <summary>
/// Atomic reference implementation for control-plane promotion.
/// A promotion is valid only when the candidate exactly matches the
/// currently authoritative state and the authority epoch advances once.
/// </summary>
public sealed record AuthorizationRecoveryPromotionState(
    long Epoch,
    long Sequence,
    string HeadDigest,
    string? ActiveControlPlaneId);

public enum AuthorizationRecoveryPromotionResult
{
    Promoted,
    StaleCandidate,
    EpochMismatch,
    SequenceMismatch,
    DigestMismatch,
    AlreadyAuthoritative,
    LostRace
}

public sealed class AuthorizationRecoveryControlPlanePromotionAuthority
{
    private readonly object _gate = new();
    private AuthorizationRecoveryPromotionState _state;

    public AuthorizationRecoveryControlPlanePromotionAuthority(
        AuthorizationRecoveryPromotionState initialState)
    {
        _state = initialState;
    }

    public AuthorizationRecoveryPromotionState Current
    {
        get { lock (_gate) return _state; }
    }

    public AuthorizationRecoveryPromotionResult TryPromote(
        string candidateId,
        AuthorizationRecoveryPromotionState candidate)
    {
        lock (_gate)
        {
            if (string.Equals(candidate.ActiveControlPlaneId, candidateId, StringComparison.Ordinal))
                return AuthorizationRecoveryPromotionResult.AlreadyAuthoritative;

            if (candidate.Epoch != _state.Epoch)
                return AuthorizationRecoveryPromotionResult.EpochMismatch;

            if (candidate.Sequence < _state.Sequence)
                return AuthorizationRecoveryPromotionResult.StaleCandidate;

            if (candidate.Sequence > _state.Sequence)
                return AuthorizationRecoveryPromotionResult.SequenceMismatch;

            if (!string.Equals(candidate.HeadDigest, _state.HeadDigest, StringComparison.Ordinal))
                return AuthorizationRecoveryPromotionResult.DigestMismatch;

            // The compare-and-promote occurs under one atomic critical section.
            // Exactly one candidate can replace the active control plane.
            _state = new AuthorizationRecoveryPromotionState(
                Epoch: checked(_state.Epoch + 1),
                Sequence: _state.Sequence,
                HeadDigest: _state.HeadDigest,
                ActiveControlPlaneId: candidateId);

            return AuthorizationRecoveryPromotionResult.Promoted;
        }
    }
}
