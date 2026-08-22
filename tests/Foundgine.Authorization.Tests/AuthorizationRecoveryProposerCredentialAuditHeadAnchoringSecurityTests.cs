using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryProposerCredentialAuditHeadAnchoringSecurityTests
{
    [Fact]
    public async Task Matching_head_is_accepted()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var record = await ledger.AppendAndAnchorAsync(anchor, "writer-a", "operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        await ledger.VerifyAgainstAnchorAsync(anchor);
        Assert.Equal(1, record.AuditSequence);
    }

    [Fact]
    public async Task Restored_older_valid_history_is_rejected()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        await ledger.AppendAndAnchorAsync(anchor, "writer-a", "operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var old = ledger.Records.ToArray();
        await ledger.AppendAndAnchorAsync(anchor, "writer-a", "operator-a", "fp-v2", 2, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var restored = AuthorizationRecoveryProposerCredentialAuditLedger.Restore(old);
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditHeadRollbackException>(() => restored.VerifyAgainstAnchorAsync(anchor).AsTask());
    }

    [Fact]
    public async Task Same_sequence_different_digest_is_rejected_as_fork()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var ledgerA = new AuthorizationRecoveryProposerCredentialAuditLedger();
        await ledgerA.AppendAndAnchorAsync(anchor, "writer-a", "operator-a", "fp-a", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);

        var ledgerB = new AuthorizationRecoveryProposerCredentialAuditLedger();
        ledgerB.Append("operator-b", "fp-b", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialAuditHeadForkException>(() => ledgerB.VerifyAgainstAnchorAsync(anchor).AsTask());
    }

    [Fact]
    public async Task Stale_writer_cannot_advance_anchor()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        Assert.True(await anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('1', 64), "writer-a"));
        Assert.False(await anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, new string('2', 64), "writer-b"));
    }

    [Fact]
    public async Task Concurrent_anchor_advances_have_one_winner()
    {
        var anchor = new InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor();
        var tasks = Enumerable.Range(0, 32).Select(i => anchor.TryAdvanceAsync(0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest, 1, i.ToString("x").PadLeft(64, '0'), $"writer-{i}").AsTask());
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, x => x);
        var state = await anchor.ReadAsync();
        Assert.Equal(1, state.Sequence);
    }
}
