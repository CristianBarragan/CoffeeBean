namespace Foundgine.Authorization;

/// <summary>
/// Represents the authoritative state a standby must have before promotion.
/// </summary>
public sealed record AuthorizationRecoveryStandbyState(
    long Epoch,
    long Sequence,
    string HeadDigest,
    bool IsAuthoritative);

/// <summary>
/// Result of checking whether a standby is eligible for promotion.
/// </summary>
public enum AuthorizationRecoveryStandbyPromotionResult
{
    Eligible,
    NotCaughtUp,
    NotAuthoritative,
    EpochMismatch,
    SequenceMismatch,
    DigestMismatch
}

/// <summary>
/// Enforces that a standby can only be promoted when it exactly matches
/// the currently authoritative control-plane state.
/// </summary>
public static class AuthorizationRecoveryStandbyPromotionSafety
{
    public static AuthorizationRecoveryStandbyPromotionResult CheckPromotionEligibility(
        AuthorizationRecoveryStandbyState standby,
        AuthorizationRecoveryStandbyState authoritative)
    {
        if (standby.IsAuthoritative)
            return AuthorizationRecoveryStandbyPromotionResult.NotAuthoritative;

        if (standby.Epoch != authoritative.Epoch)
            return AuthorizationRecoveryStandbyPromotionResult.EpochMismatch;

        if (standby.Sequence < authoritative.Sequence)
            return AuthorizationRecoveryStandbyPromotionResult.NotCaughtUp;

        if (standby.Sequence > authoritative.Sequence)
            return AuthorizationRecoveryStandbyPromotionResult.SequenceMismatch;

        if (!string.Equals(standby.HeadDigest, authoritative.HeadDigest, StringComparison.Ordinal))
            return AuthorizationRecoveryStandbyPromotionResult.DigestMismatch;

        return AuthorizationRecoveryStandbyPromotionResult.Eligible;
    }

    public static void RequirePromotionEligibility(
        AuthorizationRecoveryStandbyState standby,
        AuthorizationRecoveryStandbyState authoritative)
    {
        var result = CheckPromotionEligibility(standby, authoritative);
        if (result != AuthorizationRecoveryStandbyPromotionResult.Eligible)
            throw new InvalidOperationException(
                $"Standby promotion rejected: {result}.");
    }
}
