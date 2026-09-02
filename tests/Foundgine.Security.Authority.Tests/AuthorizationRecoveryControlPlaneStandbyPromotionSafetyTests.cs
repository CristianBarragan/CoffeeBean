using Xunit;
using Foundgine.Runtime.ControlPlane;

public sealed class AuthorizationRecoveryControlPlaneStandbyPromotionSafetyTests
{
    [Fact]
    public void Exact_current_state_is_eligible()
    {
        var active = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);
        var standby = new AuthorizationRecoveryStandbyState(7, 42, "abc", false);

        Assert.Equal(
            AuthorizationRecoveryStandbyPromotionResult.Eligible,
            AuthorizationRecoveryStandbyPromotionSafety.CheckPromotionEligibility(standby, active));
    }

    [Fact]
    public void Stale_sequence_cannot_be_promoted()
    {
        var active = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);
        var standby = new AuthorizationRecoveryStandbyState(7, 41, "old", false);

        Assert.Equal(
            AuthorizationRecoveryStandbyPromotionResult.NotCaughtUp,
            AuthorizationRecoveryStandbyPromotionSafety.CheckPromotionEligibility(standby, active));
    }

    [Fact]
    public void Same_sequence_with_different_digest_cannot_be_promoted()
    {
        var active = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);
        var standby = new AuthorizationRecoveryStandbyState(7, 42, "forged", false);

        Assert.Equal(
            AuthorizationRecoveryStandbyPromotionResult.DigestMismatch,
            AuthorizationRecoveryStandbyPromotionSafety.CheckPromotionEligibility(standby, active));
    }

    [Fact]
    public void Different_epoch_cannot_be_promoted()
    {
        var active = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);
        var standby = new AuthorizationRecoveryStandbyState(6, 42, "abc", false);

        Assert.Equal(
            AuthorizationRecoveryStandbyPromotionResult.EpochMismatch,
            AuthorizationRecoveryStandbyPromotionSafety.CheckPromotionEligibility(standby, active));
    }

    [Fact]
    public void Standby_that_claims_to_be_authoritative_cannot_be_promoted()
    {
        var active = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);
        var standby = new AuthorizationRecoveryStandbyState(7, 42, "abc", true);

        Assert.Equal(
            AuthorizationRecoveryStandbyPromotionResult.NotAuthoritative,
            AuthorizationRecoveryStandbyPromotionSafety.CheckPromotionEligibility(standby, active));
    }
}
