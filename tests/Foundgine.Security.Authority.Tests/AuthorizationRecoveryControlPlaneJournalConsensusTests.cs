using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlaneJournalConsensusTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i + 21)).ToArray();

    private static AuthorizationRecoveryTransactionJournalEntry[] Journal(string tx = "tx-1")
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(Key);
        journal.Append(tx, 7, 8, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        journal.Append(tx, 7, 8, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Committed, "fp-a");
        return journal.Entries.ToArray();
    }

    private static AuthorizationRecoveryJournalReplicaSnapshot Replica(
        string id, long revision = 8, string fingerprint = "state-a", IReadOnlyList<AuthorizationRecoveryTransactionJournalEntry>? entries = null) =>
        new(id, revision, fingerprint, entries ?? Journal());

    [Fact]
    public void Identical_authenticated_replicas_establish_consensus()
    {
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);
        var result = verifier.TryEstablishConsensus(
            new[] { Replica("A"), Replica("B"), Replica("C") }, out var consensus);

        Assert.Equal(AuthorizationRecoveryJournalConsensusResult.ConsensusEstablished, result);
        Assert.NotNull(consensus);
        Assert.Equal(8, consensus!.Revision);
        Assert.Equal("tx-1", consensus.LastCommittedTransactionId);
    }

    [Fact]
    public void Divergent_journal_heads_fail_closed()
    {
        var divergent = Journal("tx-forged");
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);

        var result = verifier.TryEstablishConsensus(
            new[] { Replica("A"), Replica("B", entries: divergent) }, out _);

        Assert.Equal(AuthorizationRecoveryJournalConsensusResult.DivergentHistory, result);
        Assert.Null(verifier.Current);
    }

    [Fact]
    public void Divergent_state_fingerprint_fails_closed_even_when_journals_match()
    {
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);
        var result = verifier.TryEstablishConsensus(
            new[] { Replica("A"), Replica("B", fingerprint: "forged-state") }, out _);

        Assert.Equal(AuthorizationRecoveryJournalConsensusResult.DivergentState, result);
    }

    [Fact]
    public void Stale_replica_cannot_reuse_previous_consensus()
    {
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);
        Assert.Equal(
            AuthorizationRecoveryJournalConsensusResult.ConsensusEstablished,
            verifier.TryEstablishConsensus(new[] { Replica("A"), Replica("B") }, out _));

        var stale = Replica("A", revision: 7);
        Assert.Equal(
            AuthorizationRecoveryJournalConsensusResult.StaleReplica,
            verifier.ValidateRecoveryAgainstConsensus(stale));
    }

    [Fact]
    public void Tampered_journal_entry_is_rejected_before_consensus()
    {
        var tampered = Journal();
        tampered[0] = tampered[0] with { TargetFingerprint = "forged" };
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);

        Assert.Equal(
            AuthorizationRecoveryJournalConsensusResult.RejectedInvalidJournal,
            verifier.TryEstablishConsensus(new[] { Replica("A", entries: tampered), Replica("B") }, out _));
    }

    [Fact]
    public void Fifty_two_concurrent_consensus_attempts_never_accept_divergence()
    {
        var verifier = new AuthorizationRecoveryControlPlaneJournalConsensus(Key);
        var divergent = Journal("tx-divergent");
        var results = new ConcurrentBag<AuthorizationRecoveryJournalConsensusResult>();

        Parallel.For(0, 52, i =>
        {
            var replicas = i % 2 == 0
                ? new[] { Replica("A"), Replica("B"), Replica("C") }
                : new[] { Replica("A"), Replica("B", entries: divergent), Replica("C") };
            results.Add(verifier.TryEstablishConsensus(replicas, out _));
        });

        Assert.DoesNotContain(results, r => r == AuthorizationRecoveryJournalConsensusResult.ConsensusEstablished && results.Count(x => x == AuthorizationRecoveryJournalConsensusResult.DivergentHistory) == 0);
        Assert.Contains(AuthorizationRecoveryJournalConsensusResult.DivergentHistory, results);
    }
}
