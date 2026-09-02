using Foundgine.Runtime.ControlPlane;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Runtime.ControlPlane.Tests;

public sealed class AuthorizationRecoveryReconfigurationProposerAuthenticationSecurityTests
{
    private static (ReconfigurableAuthorizationRecoveryQuorumAnchor Anchor, InMemoryAuthorizationRecoveryForkAnchor Primary) Cluster()
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var witnesses = Enumerable.Range(0, 3).Select(i => new AuthorizationRecoveryQuorumWitness($"w{i}", primary)).ToArray();
        var auth = new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["operator-a"] = "secret-fp-a" });
        return (new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, auth), primary);
    }

    private static AuthorizationRecoveryReconfigurationProposerCredential Credential(long version, IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses,
        string id = "operator-a", string fingerprint = "secret-fp-a") =>
        new(id, fingerprint, version, AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(witnesses));

    [Fact]
    public async Task Missing_proposer_credential_fails_closed()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next);
        Assert.False(result.Reconfigured);
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
        Assert.Equal(0, anchor.CurrentConfiguration.ConfigVersion);
    }

    [Fact]
    public async Task Unknown_proposer_is_rejected()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next, "attacker", "secret-fp-a"));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Forged_credential_fingerprint_is_rejected()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next, fingerprint: "forged"));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Credential_from_another_configuration_version_is_rejected()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(99, next));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Credential_bound_to_another_membership_is_rejected()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var other = new[] { new AuthorizationRecoveryQuorumWitness("other", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, other));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Valid_proposer_credential_allows_reconfiguration()
    {
        var (anchor, primary) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next));
        Assert.True(result.Reconfigured);
        Assert.Equal(1, anchor.CurrentConfiguration.ConfigVersion);
        Assert.Equal("operator-a", anchor.Ledger.Records[^1].ProposerId);
    }

    [Fact]
    public async Task Credential_cannot_be_reused_for_a_different_membership()
    {
        var (anchor, primary) = Cluster();
        var first = new[] { new AuthorizationRecoveryQuorumWitness("new-a", primary) };
        var second = new[] { new AuthorizationRecoveryQuorumWitness("new-b", primary) };
        var credential = Credential(0, first);
        var firstResult = await anchor.TryReconfigureAsync(0, first, credential);
        Assert.True(firstResult.Reconfigured);
        var secondResult = await anchor.TryReconfigureAsync(1, second, credential with { ExpectedConfigVersion = 1 });
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, secondResult.Outcome);
        Assert.Equal(1, anchor.CurrentConfiguration.ConfigVersion);
    }
}
