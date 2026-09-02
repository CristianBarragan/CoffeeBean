namespace Foundgine.Runtime.ControlPlane;

/// <summary>
/// Durable publication observed during recovery. The publication itself is
/// authoritative; recovery never infers state from local process memory.
/// </summary>
public sealed record AuthorizationRecoveryPromotionRecoverySnapshot(
    long Epoch,
    string ActiveControlPlaneId,
    long Sequence,
    string HeadDigest);

public enum AuthorizationRecoveryPromotionRecoveryResult
{
    RecoveredOldState,
    RecoveredCommittedState,
    NoAuthoritativePublication,
    PublicationCorrupt,
    ConflictingPublication
}

/// <summary>
/// Reference recovery reconciler. Recovery reads the authoritative durable
/// publication and deterministically resumes from it; an unknown local
/// transaction outcome is never treated as permission to promote again.
/// </summary>
public static class AuthorizationRecoveryControlPlanePromotionRecovery
{
    public static AuthorizationRecoveryPromotionRecoveryResult Reconcile(
        AuthorizationRecoveryPromotionRecoverySnapshot? durablePublication,
        AuthorizationRecoveryPromotionRecoverySnapshot? localBeforeCrash,
        AuthorizationRecoveryPromotionRecoverySnapshot? localAfterCrash)
    {
        if (durablePublication is null)
            return AuthorizationRecoveryPromotionRecoveryResult.NoAuthoritativePublication;

        if (string.IsNullOrWhiteSpace(durablePublication.ActiveControlPlaneId) ||
            string.IsNullOrWhiteSpace(durablePublication.HeadDigest) ||
            durablePublication.Epoch < 0 ||
            durablePublication.Sequence < 0)
            return AuthorizationRecoveryPromotionRecoveryResult.PublicationCorrupt;

        // Durable publication is the sole source of truth after restart.
        // Local snapshots are diagnostic only and cannot create authority.
        if (localAfterCrash is not null &&
            localAfterCrash.Epoch > durablePublication.Epoch)
            return AuthorizationRecoveryPromotionRecoveryResult.ConflictingPublication;

        if (localBeforeCrash is not null &&
            durablePublication.Epoch == localBeforeCrash.Epoch &&
            string.Equals(
                durablePublication.ActiveControlPlaneId,
                localBeforeCrash.ActiveControlPlaneId,
                StringComparison.Ordinal))
            return AuthorizationRecoveryPromotionRecoveryResult.RecoveredOldState;

        return AuthorizationRecoveryPromotionRecoveryResult.RecoveredCommittedState;
    }
}
