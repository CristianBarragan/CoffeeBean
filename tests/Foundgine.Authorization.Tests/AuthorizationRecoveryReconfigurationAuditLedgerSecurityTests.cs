using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryReconfigurationAuditLedgerSecurityTests
{
    private static (IAuthorizationRecoveryForkAnchor Primary, ReconfigurableAuthorizationRecoveryQuorumAnchor Quorum)
        MakeCluster(int witnessCount = 3)
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var witnesses = Enumerable.Range(0, witnessCount)
            .Select(i => new AuthorizationRecoveryQuorumWitness($"witness-{i}", primary))
            .ToList();

        return (primary, new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" })));
    }

    private static AuthorizationRecoveryReconfigurationProposerCredential Proposer(
        long version, IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses, string id = "operator-1", string fingerprint = "fp-1") =>
        new(id, fingerprint, version, AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(witnesses));

    [Fact]
    public void Constructing_an_anchor_writes_a_verifiable_genesis_ledger_record()
    {
        var (_, quorum) = MakeCluster();

        var records = quorum.Ledger.Records;

        Assert.Single(records);
        Assert.Equal(0, records[0].ConfigVersion);
        Assert.Equal(AuthorizationRecoveryReconfigurationLedger.GenesisPreviousDigest, records[0].PreviousRecordDigest);
        Assert.True(quorum.Ledger.VerifyChain().Verified);
    }

    [Fact]
    public async Task Every_accepted_reconfiguration_extends_the_ledger_and_the_chain_still_verifies()
    {
        var (primary, quorum) = MakeCluster();

        await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }, proposerCredential: Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }, "control-plane-1", "fp-1"));
        await quorum.TryReconfigureAsync(1, new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-new-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-new-1", primary),
        }, proposerCredential: Proposer(1, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary), new AuthorizationRecoveryQuorumWitness("witness-new-1", primary) }, "control-plane-2", "fp-1"));

        var records = quorum.Ledger.Records;

        Assert.Equal(3, records.Count); // genesis + two reconfigurations
        Assert.Equal(new long[] { 0, 1, 2 }, records.Select(r => r.ConfigVersion));
        Assert.Equal("control-plane-1", records[1].ProposerId);
        Assert.Equal("control-plane-2", records[2].ProposerId);
        Assert.True(quorum.Ledger.VerifyChain().Verified);
    }

    [Fact]
    public async Task Refused_reconfiguration_attempts_never_appear_in_the_ledger()
    {
        var (primary, quorum) = MakeCluster();

        // Stale version: refused, must not be recorded.
        await quorum.TryReconfigureAsync(99, new[] { new AuthorizationRecoveryQuorumWitness("attacker", primary) }, Proposer(99, new[] { new AuthorizationRecoveryQuorumWitness("attacker", primary) }));
        // Invalid membership: refused, must not be recorded.
        await quorum.TryReconfigureAsync(0, Array.Empty<AuthorizationRecoveryQuorumWitness>(), Proposer(0, Array.Empty<AuthorizationRecoveryQuorumWitness>()));

        Assert.Single(quorum.Ledger.Records); // only the genesis record
    }

    [Fact]
    public void Altering_a_past_records_membership_digest_breaks_verification()
    {
        var (_, quorum) = MakeCluster();
        var tampered = quorum.Ledger.Records
            .Select(r => r with { MembershipDigest = "ff" + r.MembershipDigest[2..] })
            .ToList();

        var result = AuthorizationRecoveryReconfigurationLedger.VerifyChain(tampered);

        Assert.False(result.Verified);
        Assert.Equal(AuthorizationRecoveryLedgerVerificationOutcome.RecordDigestMismatch, result.Outcome);
    }

    [Fact]
    public async Task Deleting_a_middle_record_produces_a_detectable_version_gap()
    {
        var (primary, quorum) = MakeCluster();
        await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }, Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }));
        await quorum.TryReconfigureAsync(1, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-1", primary) }, Proposer(1, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-1", primary) }));

        var all = quorum.Ledger.Records;
        var withMiddleDeleted = new[] { all[0], all[2] }; // drop the version-1 record

        var result = AuthorizationRecoveryReconfigurationLedger.VerifyChain(withMiddleDeleted);

        Assert.False(result.Verified);
        Assert.Equal(AuthorizationRecoveryLedgerVerificationOutcome.VersionGap, result.Outcome);
    }

    [Fact]
    public async Task Reordering_two_records_breaks_the_previous_digest_chain()
    {
        var (primary, quorum) = MakeCluster();
        await quorum.TryReconfigureAsync(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }, Proposer(0, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-0", primary) }));
        await quorum.TryReconfigureAsync(1, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-1", primary) }, Proposer(1, new[] { new AuthorizationRecoveryQuorumWitness("witness-new-1", primary) }));

        var all = quorum.Ledger.Records;
        var reordered = new[] { all[0], all[2], all[1] };

        var result = AuthorizationRecoveryReconfigurationLedger.VerifyChain(reordered);

        Assert.False(result.Verified);
        Assert.True(
            result.Outcome is AuthorizationRecoveryLedgerVerificationOutcome.PreviousDigestMismatch
                or AuthorizationRecoveryLedgerVerificationOutcome.VersionGap);
    }

    [Fact]
    public void Forging_a_replacement_record_with_an_unrelated_previous_digest_is_detected()
    {
        var (_, quorum) = MakeCluster();
        var genesis = quorum.Ledger.Records[0];
        var forgedNext = genesis with
        {
            ConfigVersion = 1,
            PreviousRecordDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        };

        var result = AuthorizationRecoveryReconfigurationLedger.VerifyChain(new[] { genesis, forgedNext });

        Assert.False(result.Verified);
        Assert.Equal(AuthorizationRecoveryLedgerVerificationOutcome.PreviousDigestMismatch, result.Outcome);
    }

    [Fact]
    public void An_empty_ledger_reports_Empty_rather_than_Verified()
    {
        var result = AuthorizationRecoveryReconfigurationLedger.VerifyChain(Array.Empty<AuthorizationRecoveryReconfigurationAuditRecord>());

        Assert.False(result.Verified);
        Assert.Equal(AuthorizationRecoveryLedgerVerificationOutcome.Empty, result.Outcome);
    }

    [Fact]
    public void Membership_digest_is_order_independent_over_witness_ids()
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var a = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-0", primary),
            new AuthorizationRecoveryQuorumWitness("witness-1", primary),
        };
        var b = new[]
        {
            new AuthorizationRecoveryQuorumWitness("witness-1", primary),
            new AuthorizationRecoveryQuorumWitness("witness-0", primary),
        };

        Assert.Equal(
            AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(a),
            AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(b));
    }
}
