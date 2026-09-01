using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryReconfigurationProposerCredentialRevocationSecurityTests
{
    [Fact]
    public void Revocation_invalidates_the_current_generation()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        var revoked = lifecycle.Revoke("operator-a");
        Assert.Equal(AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked, revoked.State);
        Assert.Equal(2, revoked.CredentialSequence);
    }

    [Fact]
    public async Task Revoked_credential_is_rejected()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        lifecycle.Revoke("operator-a");
        var credential = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest");
        var lease = await lifecycle.TryAcquireAsync(credential);
        Assert.Null(lease);
    }

    [Fact]
    public void Revoked_generation_cannot_be_reactivated_by_rotation()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        lifecycle.Revoke("operator-a");
        Assert.Throws<InvalidOperationException>(() => lifecycle.Rotate("operator-a", "fp-v2"));
    }

    [Fact]
    public void Revoked_generation_cannot_become_verification_only_or_active()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        lifecycle.Revoke("operator-a");
        Assert.Throws<InvalidOperationException>(() => lifecycle.SetVerificationOnly("operator-a"));
        Assert.Throws<InvalidOperationException>(() => lifecycle.Rotate("operator-a", "fp-v2"));
    }

    [Fact]
    public async Task Revocation_waits_for_an_existing_reconfiguration_lease()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        var credential = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest");
        await using var lease = await lifecycle.TryAcquireAsync(credential);
        Assert.NotNull(lease);

        var revoke = Task.Run(() => lifecycle.Revoke("operator-a"));
        await Task.Delay(25);
        Assert.False(revoke.IsCompleted);

        await lease!.DisposeAsync();
        var snapshot = await revoke;
        Assert.Equal(AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked, snapshot.State);
        Assert.Equal(2, snapshot.CredentialSequence);
    }

    [Fact]
    public async Task Old_credential_cannot_reappear_after_revocation()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        lifecycle.Revoke("operator-a");
        var old = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest", CredentialSequence: 1);
        Assert.Null(await lifecycle.TryAcquireAsync(old));
    }
}
