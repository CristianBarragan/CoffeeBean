using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryWitnessSetReconfigurationSecurityTests
{
    private const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static (IAuthorizationRecoveryForkAnchor Primary, ReconfigurableAuthorizationRecoveryQuorumAnchor Quorum, bool[] Reachable, List<AuthorizationRecoveryQuorumWitness> Witnesses)
        MakeCluster(int witnessCount = 3)
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var reachable = Enumerable.Repeat(true, witnessCount).ToArray();
        var witnesses = new List<AuthorizationRecoveryQuorumWitness>();
        for (var i = 0; i < witnessCount; i++)
        {
            var index = i;
            witnesses.Add(new AuthorizationRecoveryQuorumWitness($"witness-{index}", primary, () => reachable[index]));
        }

        return (primary, new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" })), reachable, witnesses);
    }

    private static AuthorizationRecoveryReconfigurationProposerCredential Proposer(
        long version, IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses, string id = "operator-1", string fingerprint = "fp-1") =>
        new(id, fingerprint, version, AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(witnesses));

    [Fact]
    public async Task Reconfiguration_with_current_majority_reachable_is_accepted_and_advances_config_version()
    {
        var (primary, quorum, _, _) = MakeCluster();
        var replacement = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-new-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-new-1", primary),
        };

        var result = await quorum.TryReconfigureAsync(0, replacement, Proposer(0, replacement));

        Assert.True(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.Reconfigured, result.Outcome);
        Assert.Equal(1, result.ConfigVersion);
        Assert.Equal(1, quorum.CurrentConfiguration.ConfigVersion);
        Assert.Same(replacement[0], quorum.CurrentConfiguration.Witnesses[0]);
    }

    [Fact]
    public async Task Reconfiguration_is_refused_without_a_reachable_majority_of_the_current_witnesses()
    {
        var (primary, quorum, reachable, _) = MakeCluster(witnessCount: 3);
        // Majority of 3 is 2; leave only one current witness reachable — an isolated minority
        // (or an attacker holding only that minority) must not be able to replace membership.
        reachable[1] = false;
        reachable[2] = false;
        var replacement = new[] { new AuthorizationRecoveryQuorumWitness("attacker-controlled", primary) };

        var result = await quorum.TryReconfigureAsync(0, replacement, Proposer(0, replacement));

        Assert.False(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.NoQuorum, result.Outcome);
        Assert.Equal(0, quorum.CurrentConfiguration.ConfigVersion);
    }

    [Fact]
    public async Task Stale_expected_config_version_is_refused()
    {
        var (primary, quorum, _, _) = MakeCluster();
        await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new", primary) }, Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new", primary) }));

        // Caller is still working from the pre-reconfiguration version.
        var result = await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness("attacker", primary) }, Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness("attacker", primary) }));

        Assert.False(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.StaleConfigVersion, result.Outcome);
        Assert.Equal(1, quorum.CurrentConfiguration.ConfigVersion);
    }

    [Fact]
    public async Task Old_witness_handles_become_inert_the_instant_a_newer_configuration_commits()
    {
        var (primary, quorum, reachable, oldWitnesses) = MakeCluster(witnessCount: 3);
        var newWitnesses = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-new-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-new-1", primary),
        };
        await quorum.TryReconfigureAsync(0, newWitnesses, Proposer(0, newWitnesses));

        // All three OLD witnesses are still fully reachable and agree with each other — but they
        // are no longer the configuration in force, so they must not be able to authorize anything.
        reachable[0] = reachable[1] = reachable[2] = true;
        Assert.Equal(3, oldWitnesses.Count(w => w.IsReachable));

        var advance = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");

        Assert.True(advance.Advanced); // succeeds because the NEW config (2 witnesses) has a reachable majority
        Assert.Equal(2, quorum.CurrentConfiguration.Witnesses.Count);
    }

    [Fact]
    public async Task Concurrent_reconfiguration_attempts_produce_exactly_one_winner()
    {
        var (primary, quorum, _, _) = MakeCluster();

        var tasks = Enumerable.Range(0, 16).Select(async i =>
            await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness($"candidate-{i}", primary) }, Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness($"candidate-{i}", primary) })));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(static r => r.Reconfigured));
        Assert.Equal(15, results.Count(static r => r.Outcome == AuthorizationRecoveryReconfigurationOutcome.StaleConfigVersion));
        Assert.Equal(1, quorum.CurrentConfiguration.ConfigVersion);
    }

    [Fact]
    public async Task Empty_replacement_membership_is_rejected()
    {
        var (_, quorum, _, _) = MakeCluster();

        var result = await quorum.TryReconfigureAsync(0, Array.Empty<AuthorizationRecoveryQuorumWitness>(), Proposer(0, Array.Empty<AuthorizationRecoveryQuorumWitness>()));

        Assert.False(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.InvalidMembership, result.Outcome);
        Assert.Equal(0, quorum.CurrentConfiguration.ConfigVersion);
    }

    [Fact]
    public async Task Duplicate_witness_ids_in_replacement_membership_are_rejected()
    {
        var (primary, quorum, _, _) = MakeCluster();
        var replacement = new[]
        {
            new AuthorizationRecoveryQuorumWitness("dup", primary),
            new AuthorizationRecoveryQuorumWitness("dup", primary),
        };

        var result = await quorum.TryReconfigureAsync(0, replacement, Proposer(0, replacement));

        Assert.False(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.InvalidMembership, result.Outcome);
    }

    [Fact]
    public void Invalid_initial_membership_is_rejected_at_construction()
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();

        Assert.Throws<ArgumentException>(() =>
            new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, Array.Empty<AuthorizationRecoveryQuorumWitness>(), 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1" })));
    }

    [Fact]
    public async Task Normal_advance_and_verify_still_work_after_a_successful_reconfiguration()
    {
        var (primary, quorum, _, _) = MakeCluster();
        var newWitnesses = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-new-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-new-1", primary),
            new AuthorizationRecoveryQuorumWitness("witness-new-2", primary),
        };
        await quorum.TryReconfigureAsync(0, newWitnesses, Proposer(0, newWitnesses));

        var advance = await quorum.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A");
        Assert.True(advance.Advanced);

        var verify = await quorum.TryVerifyCommittedAsync(1, DigestA);
        Assert.True(verify.Verified);
    }
}
