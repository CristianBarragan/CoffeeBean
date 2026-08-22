using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSecurityTests
{
    private static (ReconfigurableAuthorizationRecoveryQuorumAnchor Anchor, InMemoryAuthorizationRecoveryForkAnchor Primary, FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer Auth) Cluster()
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        var witnesses = Enumerable.Range(0, 3).Select(i => new AuthorizationRecoveryQuorumWitness($"w{i}", primary)).ToArray();
        var auth = new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["operator-a"] = "fp-v1" });
        return (new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, auth), primary, auth);
    }

    private static AuthorizationRecoveryReconfigurationProposerCredential Credential(long version, IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses, string fingerprint = "fp-v1", long sequence = 1) =>
        new("operator-a", fingerprint, version, AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(witnesses), CredentialSequence: sequence);

    [Fact]
    public void New_credential_starts_active_at_sequence_one()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        var snapshot = lifecycle.GetSnapshot("operator-a");
        Assert.Equal(AuthorizationRecoveryReconfigurationProposerCredentialState.Active, snapshot.State);
        Assert.Equal(1, snapshot.CredentialSequence);
    }

    [Fact]
    public void Rotation_invalidates_old_generation_and_activates_new_generation()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        var rotated = lifecycle.Rotate("operator-a", "fp-v2");
        Assert.Equal(2, rotated.CredentialSequence);
        Assert.Equal("fp-v2", rotated.CredentialFingerprint);
        Assert.Equal(AuthorizationRecoveryReconfigurationProposerCredentialState.Active, rotated.State);
    }

    [Fact]
    public async Task Old_credential_after_rotation_is_rejected()
    {
        var (anchor, primary, auth) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        auth.RotateCredential("operator-a", "fp-v2");
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task New_credential_generation_is_accepted_after_rotation()
    {
        var (anchor, primary, auth) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        auth.RotateCredential("operator-a", "fp-v2");
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next, "fp-v2", 2));
        Assert.True(result.Reconfigured);
    }

    [Fact]
    public async Task Verification_only_credential_cannot_reconfigure()
    {
        var (anchor, primary, auth) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        auth.SetVerificationOnly("operator-a");
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Retired_credential_cannot_reconfigure_or_be_reactivated()
    {
        var (anchor, primary, auth) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        auth.RetireCredential("operator-a");
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
        Assert.Throws<InvalidOperationException>(() => auth.RotateCredential("operator-a", "fp-v2"));
    }

    [Fact]
    public async Task Credential_sequence_substitution_is_rejected()
    {
        var (anchor, primary, auth) = Cluster();
        var next = new[] { new AuthorizationRecoveryQuorumWitness("new", primary) };
        var result = await anchor.TryReconfigureAsync(0, next, Credential(0, next, "fp-v1", 2));
        Assert.Equal(AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, result.Outcome);
    }

    [Fact]
    public async Task Credential_rotation_waits_for_inflight_reconfiguration_lease()
    {
        var lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "fp-v1");
        var credential = new AuthorizationRecoveryReconfigurationProposerCredential("operator-a", "fp-v1", 0, "digest");
        await using var lease = await lifecycle.TryAcquireAsync(credential);
        Assert.NotNull(lease);

        var rotation = Task.Run(() => lifecycle.Rotate("operator-a", "fp-v2"));
        await Task.Delay(25);
        Assert.False(rotation.IsCompleted);
        await lease!.DisposeAsync();
        var snapshot = await rotation;
        Assert.Equal(2, snapshot.CredentialSequence);
    }
}
