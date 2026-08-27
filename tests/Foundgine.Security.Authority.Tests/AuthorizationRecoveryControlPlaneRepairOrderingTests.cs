using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairOrderingTests
{
    [Fact]
    public void Commit_is_monotonic_and_idempotent()
    {
        var control = new AuthorizationRecoveryControlPlaneRepairOrdering(20, "fp20", "h20");
        var first = control.Commit("repair-20-21", 20, "fp20", "h20", 21, "fp21", "h21");
        var replay = control.Commit("repair-20-21", 20, "fp20", "h20", 21, "fp21", "h21");

        Assert.Equal(AuthorizationRecoveryRepairCommitResult.Committed, first);
        Assert.Equal(AuthorizationRecoveryRepairCommitResult.AlreadyCommitted, replay);
        Assert.Equal(21, control.Snapshot().Revision);
    }

    [Fact]
    public void Same_transaction_id_with_different_payload_is_rejected()
    {
        var control = new AuthorizationRecoveryControlPlaneRepairOrdering(20, "fp20", "h20");
        Assert.Equal(AuthorizationRecoveryRepairCommitResult.Committed,
            control.Commit("tx", 20, "fp20", "h20", 21, "fp21", "h21"));

        Assert.Equal(AuthorizationRecoveryRepairCommitResult.RejectedIdentityCollision,
            control.Commit("tx", 20, "fp20", "h20", 21, "evil", "hEvil"));
    }

    [Fact]
    public void Older_plan_cannot_be_inserted_after_newer_commit()
    {
        var control = new AuthorizationRecoveryControlPlaneRepairOrdering(20, "fp20", "h20");
        Assert.Equal(AuthorizationRecoveryRepairCommitResult.Committed,
            control.Commit("tx-a", 20, "fp20", "h20", 21, "fp21", "h21"));

        Assert.Equal(AuthorizationRecoveryRepairCommitResult.RejectedStalePlan,
            control.Commit("tx-b", 20, "fp20", "h20", 21, "fpB", "hB"));
    }

    [Fact]
    public void Cannot_skip_revision()
    {
        var control = new AuthorizationRecoveryControlPlaneRepairOrdering(20, "fp20", "h20");
        Assert.Equal(AuthorizationRecoveryRepairCommitResult.RejectedOrdering,
            control.Commit("tx", 20, "fp20", "h20", 22, "fp22", "h22"));
    }

    [Fact]
    public void Sixty_four_concurrent_commits_produce_one_winner()
    {
        var control = new AuthorizationRecoveryControlPlaneRepairOrdering(20, "fp20", "h20");
        var results = new ConcurrentBag<AuthorizationRecoveryRepairCommitResult>();
        Parallel.For(0, 64, i =>
        {
            results.Add(control.Commit($"tx-{i}", 20, "fp20", "h20", 21, $"fp-{i}", $"h-{i}"));
        });

        Assert.Equal(1, results.Count(x => x == AuthorizationRecoveryRepairCommitResult.Committed));
        Assert.Equal(63, results.Count(x => x == AuthorizationRecoveryRepairCommitResult.RejectedStalePlan));
        Assert.Equal(21, control.Snapshot().Revision);
    }
}
