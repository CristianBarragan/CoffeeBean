using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryForkSecurityTests
{
    private const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string DigestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DigestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string DigestC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task First_writer_wins_and_second_stale_branch_is_rejected()
    {
        var anchor = new InMemoryAuthorizationRecoveryForkAnchor();

        Assert.True(await anchor.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A"));
        Assert.False(await anchor.TryAdvanceAsync(0, Genesis, 1, DigestB, "instance-B"));

        var state = await anchor.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.Equal(DigestA, state.Digest);
        Assert.Equal("instance-A", state.WriterId);
    }

    [Fact]
    public async Task Same_sequence_with_different_digest_is_a_fork_and_is_rejected()
    {
        var anchor = new InMemoryAuthorizationRecoveryForkAnchor();
        Assert.True(await anchor.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A"));

        Assert.False(await anchor.TryAdvanceAsync(1, DigestB, 2, DigestC, "instance-B"));
        var state = await anchor.ReadAsync();
        Assert.Equal(DigestA, state.Digest);
        Assert.Equal(1, state.Sequence);
    }

    [Fact]
    public async Task Competing_instances_cannot_create_two_authoritative_children()
    {
        var anchor = new InMemoryAuthorizationRecoveryForkAnchor();
        var tasks = Enumerable.Range(0, 32).Select(async i =>
            await anchor.TryAdvanceAsync(0, Genesis, 1, i % 2 == 0 ? DigestA : DigestB, $"instance-{i}"));

        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(static value => value));

        var state = await anchor.ReadAsync();
        Assert.Equal(1, state.Sequence);
        Assert.True(state.Digest == DigestA || state.Digest == DigestB);
    }

    [Fact]
    public async Task Valid_current_branch_can_advance_after_losing_writer_refreshes_state()
    {
        var anchor = new InMemoryAuthorizationRecoveryForkAnchor();
        Assert.True(await anchor.TryAdvanceAsync(0, Genesis, 1, DigestA, "instance-A"));

        Assert.False(await anchor.TryAdvanceAsync(0, Genesis, 1, DigestB, "instance-B"));
        Assert.True(await anchor.TryAdvanceAsync(1, DigestA, 2, DigestB, "instance-B"));

        var state = await anchor.ReadAsync();
        Assert.Equal(2, state.Sequence);
        Assert.Equal(DigestB, state.Digest);
    }

    [Fact]
    public async Task Sequence_jump_is_rejected()
    {
        var anchor = new InMemoryAuthorizationRecoveryForkAnchor();
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await anchor.TryAdvanceAsync(0, Genesis, 2, DigestA, "instance-A"));
    }
}
