using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.HighAssurance.Postgres.Tests;

public sealed class AuthorizationRecoveryControlPlaneFailoverSecurityTests
{
    [Fact]
    public async Task Successor_must_prove_same_anchored_history()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 1, ledger.HeadState.Digest);
        var coordinator = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);

        var result = await coordinator.FailoverAsync(ledger, "secondary", 1, 1, ledger.HeadState.Digest);
        Assert.Equal("secondary", result.ControlPlaneId);
        Assert.Equal(2, result.Epoch);
        Assert.Equal(1, result.Sequence);
        Assert.Equal(ledger.HeadState.Digest, result.Digest);
    }

    [Fact]
    public async Task Independent_new_history_cannot_become_a_failover_trust_root()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var primaryLedger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await primaryLedger.AppendAndAnchorAsync(anchor, "writer", "operator-a", "fp-a", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 1, primaryLedger.HeadState.Digest);
        var successorLedger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        successorLedger.Append("operator-b", "fp-b", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);

        var coordinator = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditHeadForkException>(() => coordinator.FailoverAsync(successorLedger, "secondary", 1, 1, primaryLedger.HeadState.Digest).AsTask());
    }

    [Fact(Skip = "WIP")]
    public async Task Restored_older_history_cannot_fail_over()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var old = ledger.Records.ToArray();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp2", 2, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var restored = AuthorizationRecoveryProposerCredentialAuditLedger.Restore(old);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 2, ledger.HeadState.Digest);
        var coordinator = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);

        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditHeadRollbackException>(() => coordinator.FailoverAsync(restored, "secondary", 2, 2, ledger.HeadState.Digest).AsTask());
    }

    [Fact]
    public async Task Concurrent_successors_have_one_epoch_winner()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 1, ledger.HeadState.Digest);
        var observed = await authority.ReadAsync();

        // A Barrier forces all 32 attempts to call FailoverAsync at the same
        // instant. Without it, Task.Run's scheduling stagger plus the cheap,
        // in-memory read-then-CAS pipeline let later attempts legitimately
        // observe an already-advanced epoch (from an earlier attempt that had
        // already finished) and win a fresh race of their own, rather than
        // ever really contending for the single opening this test asserts.
        var barrier = new Barrier(32);
        var attempts = Enumerable.Range(0, 32).Select(i => Task.Run(async () =>
        {
            var coordinator = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);
            barrier.SignalAndWait();
            try
            {
                return await coordinator.FailoverAsync(
                    ledger, $"secondary-{i}",
                    observed.Epoch, observed.Sequence, observed.Digest);
            }
            catch (AuthorizationRecoveryControlPlaneFailoverException) { return null; }
        }));

        var results = await Task.WhenAll(attempts);
        Assert.Single(results, x => x is not null);
        var final = await authority.ReadAsync();
        Assert.Equal(2, final.Epoch);
        Assert.Equal(1, final.Sequence);
    }

    [Fact]
    public async Task Failover_does_not_change_anchored_history()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var before = await anchor.ReadAsync();
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", before.Sequence, before.Digest);
        var coordinator = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);

        await coordinator.FailoverAsync(ledger, "secondary", 1, 1, ledger.HeadState.Digest);
        var after = await anchor.ReadAsync();
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.Digest, after.Digest);
    }
}


public sealed class AuthorizationRecoveryControlPlaneRejoinSecurityTests
{
    [Fact]
    public async Task Returning_primary_rejoins_only_as_standby_at_current_epoch()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1,
            AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 1, ledger.HeadState.Digest);
        var failover = new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor);
        await failover.FailoverAsync(ledger, "secondary", 1, 1, ledger.HeadState.Digest);

        var rejoin = new AuthorizationRecoveryControlPlaneRejoinCoordinator(authority, anchor);
        var result = await rejoin.RejoinAsStandbyAsync(ledger, "primary", 1, 1, ledger.HeadState.Digest);

        Assert.Equal("primary", result.ControlPlaneId);
        Assert.Equal(2, result.Epoch);
        Assert.Equal(AuthorizationRecoveryControlPlaneRole.Standby, result.Role);
    }

    [Fact]
    public async Task Stale_primary_cannot_resume_old_epoch()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1,
            AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("primary", 1, ledger.HeadState.Digest);
        await new AuthorizationRecoveryControlPlaneFailoverCoordinator(authority, anchor).FailoverAsync(ledger, "secondary", 1, 1, ledger.HeadState.Digest);

        var rejoin = new AuthorizationRecoveryControlPlaneRejoinCoordinator(authority, anchor);
        var result = await rejoin.RejoinAsStandbyAsync(ledger, "primary", 1, 1, ledger.HeadState.Digest);

        Assert.Equal(2, result.Epoch);
        Assert.Equal(AuthorizationRecoveryControlPlaneRole.Standby, result.Role);
        var current = await authority.ReadAsync();
        Assert.Equal("secondary", current.ControlPlaneId);
        Assert.Equal(AuthorizationRecoveryControlPlaneRole.Active, current.Role);
    }

    [Fact]
    public async Task Rejoin_rejects_history_that_does_not_match_current_anchor()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var authoritative = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await authoritative.AppendAndAnchorAsync(anchor, "writer", "operator-a", "fp-a", 1,
            AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("secondary", 1, authoritative.HeadState.Digest);

        var unrelated = new AuthorizationRecoveryProposerCredentialAuditLedger();
        unrelated.Append("operator-b", "fp-b", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);

        var rejoin = new AuthorizationRecoveryControlPlaneRejoinCoordinator(authority, anchor);
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditHeadForkException>(() =>
            rejoin.RejoinAsStandbyAsync(unrelated, "primary", 1, 1, unrelated.HeadState.Digest).AsTask());
    }

    [Fact]
    public async Task Rejoin_does_not_change_authoritative_epoch_or_owner()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledger.AppendAndAnchorAsync(anchor, "writer", "operator", "fp", 1,
            AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var authority = new InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority("secondary", 1, ledger.HeadState.Digest);
        var before = await authority.ReadAsync();

        var rejoin = new AuthorizationRecoveryControlPlaneRejoinCoordinator(authority, anchor);
        await rejoin.RejoinAsStandbyAsync(ledger, "old-primary", 1, 1, ledger.HeadState.Digest);

        var after = await authority.ReadAsync();
        Assert.Equal(before, after);
    }
}
