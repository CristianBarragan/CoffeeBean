using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryWitnessCredentialLifecycleSecurityTests
{
    [Fact]
    public void CurrentCredentialAuthenticates()
    {
        var lifecycle = New();
        var auth = new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle);
        Assert.True(auth.Authenticate("w1", new("w1", "fp-1")));
    }

    [Fact]
    public void RotationInvalidatesOldCredentialAndAdvancesGeneration()
    {
        var lifecycle = New();
        Assert.True(lifecycle.TryRotate("w1", "fp-2", 1, out var seq));
        Assert.Equal(2, seq);
        Assert.False(new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle).Authenticate("w1", new("w1", "fp-1")));
        Assert.True(new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle).Authenticate("w1", new("w1", "fp-2")));
    }

    [Fact]
    public void StaleRotationCannotWin()
    {
        var lifecycle = New();
        Assert.True(lifecycle.TryRotate("w1", "fp-2", 1, out _));
        Assert.False(lifecycle.TryRotate("w1", "fp-attacker", 1, out _));
    }

    [Fact]
    public async Task RevocationImmediatelyInvalidatesLease()
    {
        var lifecycle = New();
        await using var lease = await lifecycle.TryAcquireAsync(new("w1", "fp-1"));
        Assert.NotNull(lease);
        Assert.True(lifecycle.TryRevoke("w1", 1));
        Assert.False(await lease!.ValidateStillCurrentAsync());
        Assert.False(new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle).Authenticate("w1", new("w1", "fp-1")));
    }

    [Fact]
    public void VerificationOnlyOverlapCanValidateInflightLeaseButCannotAuthenticateNormally()
    {
        var lifecycle = New();
        // Simulate an overlap state exposed by the control plane without changing the credential secret.
        Assert.True(lifecycle.TryRotate("w1", "fp-2", 1, out _));
        var snapshot = lifecycle.GetSnapshot("w1");
        Assert.Equal(AuthorizationRecoveryWitnessCredentialState.Active, snapshot.State);
        Assert.False(new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle).Authenticate("w1", new("w1", "fp-1")));
    }

    [Fact]
    public void RevokedCredentialCannotBeReactivatedByStaleRotation()
    {
        var lifecycle = New();
        Assert.True(lifecycle.TryRevoke("w1", 1));
        Assert.False(lifecycle.TryRotate("w1", "fp-2", 1, out _));
    }

    [Fact]
    public void UnknownWitnessFailsClosed()
    {
        var lifecycle = New();
        Assert.False(new LifecycleAuthorizationRecoveryWitnessCredentialAuthenticator(lifecycle).Authenticate("missing", new("missing", "fp")));
    }

    private static AuthorizationRecoveryWitnessCredentialLifecycle New()
    {
        var lifecycle = new AuthorizationRecoveryWitnessCredentialLifecycle();
        lifecycle.Register("w1", "fp-1");
        return lifecycle;
    }
}
