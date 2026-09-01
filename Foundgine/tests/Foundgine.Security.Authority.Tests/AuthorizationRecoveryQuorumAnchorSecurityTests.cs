using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryQuorumAnchorSecurityTests
{
    private const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>
    /// Three witnesses that all observe the same primary, plus per-witness reachability toggles the
    /// test controls directly to simulate partitions without any network involved.
    /// </summary>
    private static (IAuthorizationRecoveryForkAnchor Primary, QuorumAuthorizationRecoveryForkAnchor Quorum, bool[] Reachable) MakeCluster(int witnessCount = 3)
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var reachable = Enumerable.Repeat(true, witnessCount).ToArray();
        var witnesses = new List<AuthorizationRecoveryQuorumWitness>();
        for (var i = 0; i < witnessCount; i++)
        {
            var index = i;
            // Witnesses observe the same primary directly, modelling healthy, caught-up replicas.
            witnesses.Add(new AuthorizationRecoveryQuorumWitness($"witness-{index}", primary, () => reachable[index]));
        }

        return (primary, new QuorumAuthorizationRecoveryForkAnchor(primary, witnesses), reachable);
    }

    [Fact]
    public async Task Quorum_present_and_agreeing_permits_the_single_authoritative_write()
    {
        var (primary, quorum, _) = MakeCluster();

        var result = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        Assert.True(result.Advanced);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.Available, result.Availability);
        var state = await primary.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.Equal(DigestA, state.Digest);
    }

    [Fact]
    public async Task No_quorum_refuses_to_create_new_authority_and_never_touches_the_primary()
    {
        var (primary, quorum, reachable) = MakeCluster(witnessCount: 3);
        // Majority of 3 is 2; leave only one witness reachable.
        reachable[1] = false;
        reachable[2] = false;

        var result = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        Assert.False(result.Advanced);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.NoQuorum, result.Availability);

        // The primary must remain completely untouched: no partial write, no attempted write.
        var state = await primary.ReadAsync();
        Assert.Equal(0, state.Sequence);
        Assert.Equal(Genesis, state.Digest);
    }

    [Fact]
    public async Task Quorum_restored_after_partition_allows_advance_again()
    {
        var (primary, quorum, reachable) = MakeCluster(witnessCount: 3);
        reachable[1] = false;
        reachable[2] = false;

        var duringPartition = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.NoQuorum, duringPartition.Availability);

        reachable[1] = true; // majority (2 of 3) reachable again
        var afterRecovery = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        Assert.True(afterRecovery.Advanced);
        var state = await primary.ReadAsync();
        Assert.Equal(1, state.Sequence);
    }

    [Fact]
    public async Task Stale_caller_state_is_rejected_without_touching_the_primary_as_a_no_op()
    {
        var (primary, quorum, _) = MakeCluster();
        Assert.True((await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A")).Advanced);

        // instance-B is unaware the anchor already moved past genesis.
        var stale = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestB, "instance-B");

        Assert.False(stale.Advanced);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.Available, stale.Availability);
        var state = await primary.ReadAsync();
        Assert.Equal(DigestA, state.Digest);
    }

    [Fact]
    public async Task Disagreeing_reachable_witnesses_are_rejected_as_a_split_symptom()
    {
        var divergedPrimary = new InMemoryAuthorizationRecoveryForkAnchor();
        var staleReplica = new InMemoryAuthorizationRecoveryForkAnchor();
        // The stale replica independently advanced (e.g. it was, incorrectly, once writable too).
        await staleReplica.TryAdvanceAsync(0, Genesis, 1, DigestB, "rogue-writer");

        var witnesses = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-0", divergedPrimary),
            new AuthorizationRecoveryQuorumWitness("witness-1", staleReplica),
            new AuthorizationRecoveryQuorumWitness("witness-2", divergedPrimary),
        };
        var quorum = new QuorumAuthorizationRecoveryForkAnchor(divergedPrimary, witnesses);

        var result = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        Assert.False(result.Advanced);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.Available, result.Availability);
        Assert.Equal(0, (await divergedPrimary.ReadAsync()).Sequence);
    }

    [Fact]
    public async Task Concurrent_writers_under_full_quorum_still_produce_exactly_one_winner()
    {
        var (primary, quorum, _) = MakeCluster();

        var tasks = Enumerable.Range(0, 32).Select(async i =>
            await quorum.TryAdvanceAsync(0, Genesis, 1, i % 2 == 0 ? DigestA : DigestB, $"instance-{i}"));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(static r => r.Advanced));
        var state = await primary.ReadAsync();
        Assert.Equal(1, state.Sequence);
    }

    [Fact]
    public async Task Already_committed_state_can_be_verified_without_creating_new_authority()
    {
        var (primary, quorum, _) = MakeCluster();
        await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        var verify = await quorum.TryVerifyCommittedAsync(1, DigestA);

        Assert.True(verify.Verified);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.Available, verify.Availability);
        // Verification must never itself advance state.
        var state = await primary.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.Equal(DigestA, state.Digest);
    }

    [Fact]
    public async Task Verification_fails_closed_as_indeterminate_when_quorum_is_lost()
    {
        var (_, quorum, reachable) = MakeCluster(witnessCount: 3);
        reachable[0] = false;
        reachable[1] = false;

        var verify = await quorum.TryVerifyCommittedAsync(0, Genesis);

        Assert.False(verify.Verified);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.NoQuorum, verify.Availability);
    }

    [Fact]
    public async Task Verification_correctly_refutes_a_superseded_checkpoint()
    {
        var (_, quorum, _) = MakeCluster();
        await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");
        await quorum.TryAdvanceAsync(1, DigestA, 2, DigestB, "instance-A");

        // Caller still believes sequence 1 / DigestA is current; the anchor has moved on.
        var verify = await quorum.TryVerifyCommittedAsync(1, DigestA);

        Assert.False(verify.Verified);
        Assert.Equal(AuthorizationRecoveryQuorumAvailability.Available, verify.Availability);
    }

    [Fact]
    public void Duplicate_witness_ids_are_rejected_at_construction()
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var witnesses = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-0", primary),
        };

        Assert.Throws<ArgumentException>(() => new QuorumAuthorizationRecoveryForkAnchor(primary, witnesses));
    }
}
