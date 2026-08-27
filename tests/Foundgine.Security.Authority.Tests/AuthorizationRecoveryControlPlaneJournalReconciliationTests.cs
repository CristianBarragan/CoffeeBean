using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlaneJournalReconciliationTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i + 31)).ToArray();

    private static AuthorizationRecoveryTransactionJournalEntry[] Journal(int count, string prefix = "tx")
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(Key);
        for (var i = 1; i <= count; i++)
        {
            var tx = $"{prefix}-{i}";
            journal.Append(tx, 6 + i - 1, 6 + i, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Prepared, $"fp-{i}");
            journal.Append(tx, 6 + i - 1, 6 + i, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Committed, $"fp-{i}");
        }
        return journal.Entries.ToArray();
    }

    private static AuthorizationRecoveryJournalReplicaSnapshot Replica(string id, long revision, string fingerprint, IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry> entries) =>
        new(id, revision, fingerprint, entries);

    [Fact]
    public void Stale_prefix_builds_explicit_repair_plan()
    {
        var authorityEntries = Journal(2);
        var localEntries = authorityEntries.Take(2).ToArray();
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        var result = reconciler.TryBuildRepairPlan(
            Replica("A", 7, "fp-1", localEntries),
            Replica("AUTH", 8, "fp-2", authorityEntries),
            out var plan);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.Reconciled, result);
        Assert.NotNull(plan);
        Assert.Equal(8, plan!.TargetRevision);
        Assert.Equal("tx-2", plan.TargetLastCommittedTransactionId);
    }

    [Fact]
    public void Already_synchronized_replica_requires_no_repair()
    {
        var entries = Journal(1);
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);
        var replica = Replica("A", 7, "fp-1", entries);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.AlreadySynchronized,
            reconciler.TryBuildRepairPlan(replica, replica, out var plan));
        Assert.Null(plan);
    }

    [Fact]
    public void Same_length_fork_is_never_overwritten()
    {
        var authorityEntries = Journal(1);
        var fork = Journal(1, "evil");
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        var result = reconciler.TryBuildRepairPlan(
            Replica("A", 7, "fp-1", fork),
            Replica("AUTH", 7, "fp-1", authorityEntries),
            out _);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.ConflictingHistory, result);
    }

    [Fact]
    public void Tampered_authoritative_history_is_rejected()
    {
        var local = Journal(1);
        var authoritative = Journal(2);
        authoritative[0] = authoritative[0] with { TransactionId = "forged" };
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.RejectedInvalidAuthoritativeJournal,
            reconciler.TryBuildRepairPlan(
                Replica("A", 7, "fp-1", local),
                Replica("AUTH", 8, "fp-2", authoritative), out _));
    }

    [Fact]
    public void Tampered_local_history_is_rejected_before_repair()
    {
        var local = Journal(1);
        local[0] = local[0] with { TargetFingerprint = "forged" };
        var authority = Journal(2);
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.RejectedInvalidLocalJournal,
            reconciler.TryBuildRepairPlan(
                Replica("A", 7, "fp-1", local),
                Replica("AUTH", 8, "fp-2", authority), out _));
    }

    [Fact]
    public void Same_history_length_with_different_state_fails_closed()
    {
        var entries = Journal(1);
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.StateMismatch,
            reconciler.TryBuildRepairPlan(
                Replica("A", 7, "wrong-state", entries),
                Replica("AUTH", 7, "fp-1", entries), out _));
    }

    [Fact]
    public void Forty_concurrent_reconciliations_never_accept_a_fork()
    {
        var authority = Journal(2);
        var local = authority.Take(2).ToArray();
        var fork = Journal(1, "fork");
        var reconciler = new AuthorizationRecoveryControlPlaneJournalReconciliation(Key);

        // Each iteration writes to its own slot, so there is no race on the
        // array itself. A ConcurrentBag was used previously, but ConcurrentBag
        // does not preserve insertion order -- its enumeration order depends on
        // per-thread internal lists and thread scheduling, not the original
        // loop index. Filtering "results.Where((_, i) => i % 2 == 0)" against
        // that enumeration order does not recover which result came from which
        // Parallel.For iteration, which made this test flaky (it was checking
        // an effectively random subset of results against the wrong expected
        // outcome on each run) rather than exercising a real race in the
        // reconciler.
        var results = new AuthorizationRecoveryJournalReconciliationResult[40];

        Parallel.For(0, 40, i =>
        {
            results[i] = i % 2 == 0
                ? reconciler.TryBuildRepairPlan(Replica($"A-{i}", 7, "fp-1", local), Replica("AUTH", 8, "fp-2", authority), out _)
                : reconciler.TryBuildRepairPlan(Replica($"F-{i}", 7, "fp-1", fork), Replica("AUTH", 8, "fp-2", authority), out _);
        });

        Assert.All(results.Where((_, i) => i % 2 == 0), r => Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.Reconciled, r));
        Assert.All(results.Where((_, i) => i % 2 != 0), r => Assert.Equal(AuthorizationRecoveryJournalReconciliationResult.ConflictingHistory, r));
    }
}