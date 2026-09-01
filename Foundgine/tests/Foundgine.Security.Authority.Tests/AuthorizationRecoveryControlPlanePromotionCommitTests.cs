using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePromotionCommitTests
{
    [Fact]
    public void Promotion_publishes_epoch_owner_and_history_as_one_state()
    {
        var store = new AuthorizationRecoveryControlPlanePromotionCommitStore(
            new AuthorizationRecoveryPromotionPublication(7, "primary", 42, "digest-A"));

        Assert.Equal(
            AuthorizationRecoveryPromotionCommitResult.Committed,
            store.TryCommit(7, "digest-A", "secondary"));

        var current = store.Current;
        Assert.Equal(8, current.Epoch);
        Assert.Equal("secondary", current.ActiveControlPlaneId);
        Assert.Equal(42, current.Sequence);
        Assert.Equal("digest-A", current.HeadDigest);
    }

    [Fact]
    public void Stale_epoch_cannot_publish_a_half_promotion()
    {
        var store = new AuthorizationRecoveryControlPlanePromotionCommitStore(
            new AuthorizationRecoveryPromotionPublication(8, "secondary", 42, "digest-A"));

        Assert.Equal(
            AuthorizationRecoveryPromotionCommitResult.StaleExpectedEpoch,
            store.TryCommit(7, "digest-A", "primary"));

        Assert.Equal("secondary", store.Current.ActiveControlPlaneId);
        Assert.Equal(8, store.Current.Epoch);
    }

    [Fact]
    public void History_mismatch_cannot_publish_promotion()
    {
        var store = new AuthorizationRecoveryControlPlanePromotionCommitStore(
            new AuthorizationRecoveryPromotionPublication(7, "primary", 42, "digest-A"));

        Assert.Equal(
            AuthorizationRecoveryPromotionCommitResult.HistoryMismatch,
            store.TryCommit(7, "digest-B", "secondary"));

        Assert.Equal("primary", store.Current.ActiveControlPlaneId);
        Assert.Equal(7, store.Current.Epoch);
    }

    [Fact]
    public void Concurrent_commits_publish_exactly_one_successor()
    {
        var store = new AuthorizationRecoveryControlPlanePromotionCommitStore(
            new AuthorizationRecoveryPromotionPublication(7, "primary", 42, "digest-A"));

        var results = new ConcurrentBag<AuthorizationRecoveryPromotionCommitResult>();

        Parallel.For(0, 32, i =>
        {
            results.Add(store.TryCommit(7, "digest-A", $"secondary-{i}"));
        });

        Assert.Equal(1, results.Count(x => x == AuthorizationRecoveryPromotionCommitResult.Committed));
        Assert.Equal(8, store.Current.Epoch);
        Assert.Equal(42, store.Current.Sequence);
        Assert.Equal("digest-A", store.Current.HeadDigest);
    }
}
