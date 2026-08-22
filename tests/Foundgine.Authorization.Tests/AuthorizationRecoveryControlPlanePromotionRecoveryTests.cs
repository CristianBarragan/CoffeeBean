using Foundgine.Authorization;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePromotionRecoveryTests
{
    [Fact]
    public void Recovery_uses_durable_old_state_when_promotion_did_not_publish()
    {
        var durable = new AuthorizationRecoveryPromotionRecoverySnapshot(
            7, "primary", 42, "digest-A");

        var localBefore = durable;
        var localAfter = new AuthorizationRecoveryPromotionRecoverySnapshot(
            7, "primary", 42, "digest-A");

        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.RecoveredOldState,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(
                durable, localBefore, localAfter));
    }

    [Fact]
    public void Recovery_uses_durable_new_state_when_promotion_published_before_crash()
    {
        var before = new AuthorizationRecoveryPromotionRecoverySnapshot(
            7, "primary", 42, "digest-A");

        var durable = new AuthorizationRecoveryPromotionRecoverySnapshot(
            8, "secondary", 42, "digest-A");

        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.RecoveredCommittedState,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(
                durable, before, null));
    }

    [Fact]
    public void Missing_durable_publication_fails_closed()
    {
        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.NoAuthoritativePublication,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(null, null, null));
    }

    [Fact]
    public void Future_local_state_cannot_override_durable_authority()
    {
        var durable = new AuthorizationRecoveryPromotionRecoverySnapshot(
            8, "secondary", 42, "digest-A");

        var local = new AuthorizationRecoveryPromotionRecoverySnapshot(
            9, "rogue", 42, "digest-A");

        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.ConflictingPublication,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(
                durable, null, local));
    }

    [Fact]
    public void Corrupt_durable_publication_fails_closed()
    {
        var corrupt = new AuthorizationRecoveryPromotionRecoverySnapshot(
            8, "", 42, "digest-A");

        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.PublicationCorrupt,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(
                corrupt, null, null));
    }

    [Fact]
    public void Recovery_does_not_treat_unknown_local_outcome_as_new_authority()
    {
        var durable = new AuthorizationRecoveryPromotionRecoverySnapshot(
            8, "secondary", 42, "digest-A");

        var staleLocal = new AuthorizationRecoveryPromotionRecoverySnapshot(
            7, "primary", 42, "digest-A");

        Assert.Equal(
            AuthorizationRecoveryPromotionRecoveryResult.RecoveredCommittedState,
            AuthorizationRecoveryControlPlanePromotionRecovery.Reconcile(
                durable, staleLocal, staleLocal));
    }
}
