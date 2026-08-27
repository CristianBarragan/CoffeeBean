namespace Foundgine.Security.Authority;

/// <summary>
/// Represents the atomically published control-plane promotion state.
/// The epoch, active owner and history head are one recoverable unit.
/// </summary>
public sealed record AuthorizationRecoveryPromotionPublication(
    long Epoch,
    string ActiveControlPlaneId,
    long Sequence,
    string HeadDigest);

public enum AuthorizationRecoveryPromotionCommitResult
{
    Committed,
    AlreadyCommitted,
    StaleExpectedEpoch,
    HistoryMismatch,
    PublicationConflict
}

/// <summary>
/// Reference implementation of an atomic promotion publication boundary.
/// Production implementations must provide the same semantics through an
/// independent durable linearizable control-plane store.
/// </summary>
public sealed class AuthorizationRecoveryControlPlanePromotionCommitStore
{
    private readonly object _gate = new();
    private AuthorizationRecoveryPromotionPublication _published;

    public AuthorizationRecoveryControlPlanePromotionCommitStore(
        AuthorizationRecoveryPromotionPublication initial)
    {
        _published = initial;
    }

    public AuthorizationRecoveryPromotionPublication Current
    {
        get { lock (_gate) return _published; }
    }

    public AuthorizationRecoveryPromotionCommitResult TryCommit(
        long expectedEpoch,
        string expectedHeadDigest,
        string newActiveControlPlaneId)
    {
        lock (_gate)
        {
            if (_published.Epoch != expectedEpoch)
                return AuthorizationRecoveryPromotionCommitResult.StaleExpectedEpoch;

            if (!string.Equals(_published.HeadDigest, expectedHeadDigest, StringComparison.Ordinal))
                return AuthorizationRecoveryPromotionCommitResult.HistoryMismatch;

            if (string.Equals(_published.ActiveControlPlaneId, newActiveControlPlaneId, StringComparison.Ordinal))
                return AuthorizationRecoveryPromotionCommitResult.AlreadyCommitted;

            _published = new AuthorizationRecoveryPromotionPublication(
                Epoch: checked(expectedEpoch + 1),
                ActiveControlPlaneId: newActiveControlPlaneId,
                Sequence: _published.Sequence,
                HeadDigest: _published.HeadDigest);

            return AuthorizationRecoveryPromotionCommitResult.Committed;
        }
    }
}
