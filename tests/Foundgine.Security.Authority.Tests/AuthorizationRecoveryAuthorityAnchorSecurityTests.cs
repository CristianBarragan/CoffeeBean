using Foundgine.Runtime.ControlPlane;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Runtime.ControlPlane.Tests;

public sealed class AuthorizationRecoveryAuthorityAnchorSecurityTests
{
    private const string Genesis = AuthorizationRecoveryAnchorState.GenesisDigest;
    private const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task Current_authority_can_advance_anchor()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var authority = await anchor.ReadAuthorityAsync();

        Assert.True(await anchor.TryAdvanceAsync(authority.Term, authority.AuthorityId, 0, Genesis, 1, DigestA));
        var state = await anchor.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.Equal("authority-A", state.WriterId);
    }

    [Fact]
    public async Task Stale_authority_is_fenced_after_term_change()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        Assert.True(await anchor.TryInstallAuthorityAsync(1, "authority-A", 2, "authority-B"));

        Assert.False(await anchor.TryAdvanceAsync(1, "authority-A", 0, Genesis, 1, DigestA));
        Assert.Equal(0, (await anchor.ReadAsync()).Sequence);
    }

    [Fact]
    public async Task New_authority_can_advance_after_failover()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        Assert.True(await anchor.TryInstallAuthorityAsync(1, "authority-A", 2, "authority-B"));

        Assert.True(await anchor.TryAdvanceAsync(2, "authority-B", 0, Genesis, 1, DigestA));
        Assert.Equal(1, (await anchor.ReadAsync()).Sequence);
    }

    [Fact]
    public async Task Stale_authority_cannot_reinstall_or_skip_a_term()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        Assert.True(await anchor.TryInstallAuthorityAsync(1, "authority-A", 2, "authority-B"));

        Assert.False(await anchor.TryInstallAuthorityAsync(1, "authority-A", 2, "authority-C"));
        Assert.False(await anchor.TryInstallAuthorityAsync(1, "authority-A", 3, "authority-C"));

        var authority = await anchor.ReadAuthorityAsync();
        Assert.Equal(2, authority.Term);
        Assert.Equal("authority-B", authority.AuthorityId);
    }

    [Fact]
    public async Task Competing_current_authority_writers_are_fenced_by_the_recovery_cas()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var authority = await anchor.ReadAuthorityAsync();

        var results = await Task.WhenAll(
            anchor.TryAdvanceAsync(authority.Term, authority.AuthorityId, 0, Genesis, 1, DigestA).AsTask(),
            anchor.TryAdvanceAsync(authority.Term, authority.AuthorityId, 0, Genesis, 1, DigestB).AsTask());

        Assert.Equal(1, results.Count(static x => x));
        Assert.Equal(1, (await anchor.ReadAsync()).Sequence);
    }

    [Fact]
    public async Task Authority_identity_is_not_sufficient_without_the_current_term()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        Assert.True(await anchor.TryInstallAuthorityAsync(1, "authority-A", 2, "authority-B"));

        Assert.False(await anchor.TryAdvanceAsync(1, "authority-B", 0, Genesis, 1, DigestA));
        Assert.Equal(0, (await anchor.ReadAsync()).Sequence);
    }
}
