using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePromotionAtomicityTests
{
    [Fact]
    public void Exactly_one_of_two_eligible_standbys_can_promote()
    {
        var authority = new AuthorizationRecoveryControlPlanePromotionAuthority(
            new AuthorizationRecoveryPromotionState(7, 42, "digest-A", "primary"));

        var candidate = new AuthorizationRecoveryPromotionState(7, 42, "digest-A", null);
        var results = new ConcurrentBag<AuthorizationRecoveryPromotionResult>();

        Parallel.For(0, 32, i =>
        {
            results.Add(authority.TryPromote($"standby-{i}", candidate));
        });

        Assert.Equal(1, results.Count(x => x == AuthorizationRecoveryPromotionResult.Promoted));
        Assert.Equal(8, authority.Current.Epoch);
        Assert.Equal(42, authority.Current.Sequence);
        Assert.Equal("digest-A", authority.Current.HeadDigest);
    }

    [Fact]
    public void Losing_candidate_cannot_become_authoritative_after_the_race()
    {
        var authority = new AuthorizationRecoveryControlPlanePromotionAuthority(
            new AuthorizationRecoveryPromotionState(7, 42, "digest-A", "primary"));

        var candidate = new AuthorizationRecoveryPromotionState(7, 42, "digest-A", null);

        Assert.Equal(
            AuthorizationRecoveryPromotionResult.Promoted,
            authority.TryPromote("standby-A", candidate));

        Assert.Equal(
            AuthorizationRecoveryPromotionResult.EpochMismatch,
            authority.TryPromote("standby-B", candidate));
    }

    [Fact]
    public void Forked_digest_cannot_promote()
    {
        var authority = new AuthorizationRecoveryControlPlanePromotionAuthority(
            new AuthorizationRecoveryPromotionState(7, 42, "digest-A", "primary"));

        var candidate = new AuthorizationRecoveryPromotionState(7, 42, "digest-B", null);

        Assert.Equal(
            AuthorizationRecoveryPromotionResult.DigestMismatch,
            authority.TryPromote("standby-B", candidate));
    }

    [Fact]
    public void Stale_sequence_cannot_promote()
    {
        var authority = new AuthorizationRecoveryControlPlanePromotionAuthority(
            new AuthorizationRecoveryPromotionState(7, 42, "digest-A", "primary"));

        var candidate = new AuthorizationRecoveryPromotionState(7, 41, "digest-old", null);

        Assert.Equal(
            AuthorizationRecoveryPromotionResult.StaleCandidate,
            authority.TryPromote("standby-old", candidate));
    }

    [Fact]
    public void Successful_promotion_advances_epoch_without_changing_history()
    {
        var authority = new AuthorizationRecoveryControlPlanePromotionAuthority(
            new AuthorizationRecoveryPromotionState(12, 99, "digest-Z", "primary"));

        var candidate = new AuthorizationRecoveryPromotionState(12, 99, "digest-Z", null);

        Assert.Equal(
            AuthorizationRecoveryPromotionResult.Promoted,
            authority.TryPromote("secondary", candidate));

        Assert.Equal(13, authority.Current.Epoch);
        Assert.Equal(99, authority.Current.Sequence);
        Assert.Equal("digest-Z", authority.Current.HeadDigest);
        Assert.Equal("secondary", authority.Current.ActiveControlPlaneId);
    }
}
