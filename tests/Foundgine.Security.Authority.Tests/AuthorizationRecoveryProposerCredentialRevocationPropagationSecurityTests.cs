using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryProposerCredentialRevocationPropagationSecurityTests
{
    [Fact]
    public async Task Revocation_in_one_instance_is_observed_by_another_instance()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        var a = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        var b = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        a.Register("operator-a", "fp-v1");

        a.Revoke("operator-a");

        var old = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest", CredentialSequence: 1);
        Assert.Null(await b.TryAcquireAsync(old));
        Assert.Equal(AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked, b.GetSnapshot("operator-a").State);
    }

    [Fact]
    public async Task Rotation_in_one_instance_is_observed_by_another_instance()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        var a = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        var b = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        a.Register("operator-a", "fp-v1");
        a.Rotate("operator-a", "fp-v2");

        var old = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest", CredentialSequence: 1);
        var current = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v2", 0, "digest", CredentialSequence: 2);
        Assert.Null(await b.TryAcquireAsync(old));
        await using var lease = await b.TryAcquireAsync(current);
        Assert.NotNull(lease);
    }

    [Fact]
    public async Task Stale_instance_cannot_resurrect_revoked_state()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        var a = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        var b = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        a.Register("operator-a", "fp-v1");
        a.Revoke("operator-a");

        Assert.Throws<InvalidOperationException>(() => b.Rotate("operator-a", "fp-v2"));
        Assert.Null(await b.TryAcquireAsync(new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest", CredentialSequence: 1)));
    }

    [Fact]
    public async Task Cross_instance_revocation_invalidates_an_already_acquired_lease_at_final_gate()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        var a = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        var b = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        a.Register("operator-a", "fp-v1");
        var credential = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest", CredentialSequence: 1);

        await using var lease = await b.TryAcquireAsync(credential);
        Assert.NotNull(lease);

        a.Revoke("operator-a");

        Assert.False(await lease!.ValidateStillCurrentAsync());
    }

    [Fact]
    public async Task Durable_store_rejects_sequence_rollback()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        await store.WriteAsync(
            new AuthorizationRecoveryProposerCredentialDurableState("operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active), 0);

        await Assert.ThrowsAsync<AuthorizationRecoveryProposerCredentialRevocationConflictException>(async () =>
            await store.WriteAsync(
                new AuthorizationRecoveryProposerCredentialDurableState("operator-a", "fp-old", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active), 0));
    }

    [Fact]
    public async Task Revocation_isolated_to_the_target_proposer()
    {
        var store = new InMemoryAuthorizationRecoveryProposerCredentialRevocationStore();
        var a = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        var b = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(store);
        a.Register("operator-a", "fp-a");
        a.Register("operator-b", "fp-b");
        a.Revoke("operator-a");

        var credentialB = new AuthorizationRecoveryReconfigurationProposerCredential("operator-b", "fp-b", 0, "digest", CredentialSequence: 1);
        await using var lease = await b.TryAcquireAsync(credentialB);
        Assert.NotNull(lease);
    }
}
