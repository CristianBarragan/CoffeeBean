using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.HighAssurance.Postgres.Tests;

public sealed class AuthorizationRecoveryProposerCredentialAuditAnchorAvailabilitySecurityTests
{
    [Fact]
    public async Task Unavailable_anchor_allows_read_but_rejects_new_authority()
    {
        var inner = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var availability = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability();
        availability.SetUnavailable("network partition");
        var anchor = new AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(inner, availability);

        var state = await anchor.ReadAsync();
        Assert.Equal(0, state.Sequence);
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException>(() =>
            anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('1', 64), "writer-a").AsTask());
    }

    [Fact]
    public async Task Degraded_anchor_fails_closed_for_advancement()
    {
        var inner = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var availability = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability();
        availability.SetDegraded("quorum unavailable");
        var anchor = new AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(inner, availability);

        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException>(() =>
            anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('2', 64), "writer-a").AsTask());
    }

    [Fact]
    public async Task Recovery_can_verify_existing_head_while_anchor_is_unavailable()
    {
        var inner = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var availability = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability();
        var anchor = new AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(inner, availability);
        Assert.True(await anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('a', 64), "writer-a"));

        availability.SetUnavailable("partition");
        var state = await anchor.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.Equal(new string('a', 64), state.Digest);
    }

    [Fact]
    public async Task Availability_restoration_allows_advancement_again()
    {
        var inner = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var availability = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability();
        var anchor = new AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(inner, availability);
        availability.SetUnavailable("timeout");
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException>(() =>
            anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('b', 64), "writer-a").AsTask());

        availability.SetAvailable();
        Assert.True(await anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('b', 64), "writer-a"));
    }

    [Fact]
    public async Task Thirty_two_partitioned_writers_create_no_new_authority()
    {
        var inner = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var availability = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability();
        availability.SetUnavailable("partition");
        var anchor = new AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(inner, availability);

        var tasks = Enumerable.Range(0, 32).Select(i => anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, i.ToString("x").PadLeft(64, '0'), $"writer-{i}").AsTask());
        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            try { return await task; }
            catch (AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException) { return false; }
        }));

        Assert.DoesNotContain(results, x => x);
        Assert.Equal(0, (await anchor.ReadAsync()).Sequence);
    }
}
