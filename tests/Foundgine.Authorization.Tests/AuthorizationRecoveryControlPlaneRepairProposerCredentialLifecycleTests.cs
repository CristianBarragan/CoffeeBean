using System.Collections.Concurrent;
using System.Security.Cryptography;
using Foundgine.Authorization;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleTests
{
    private static readonly byte[] KeyV1 = SHA256.HashData("m5.70-key-v1"u8.ToArray());
    private static readonly byte[] KeyV2 = SHA256.HashData("m5.70-key-v2"u8.ToArray());

    private static AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle Lifecycle()
    {
        var lifecycle = new AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle();
        lifecycle.Register("operator-a", "cred-v1", "fp-v1", KeyV1, 7);
        return lifecycle;
    }

    private static AuthorizationRecoveryRepairProposerCredential Credential(
        string credentialId = "cred-v1", string fingerprint = "fp-v1", long sequence = 7,
        string tx = "repair-20-21")
    {
        var c = new AuthorizationRecoveryRepairProposerCredential(
            "operator-a", credentialId, sequence, fingerprint, tx, 20, "fp20", "h20",
            21, "fp21", "h21", "", "v1");
        var key = sequence == 7 ? KeyV1 : KeyV2;
        return c with { Proof = AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle.CreateProof(c, key) };
    }

    [Fact]
    public void Current_credential_is_authorized()
    {
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized,
            Lifecycle().Authorize(Credential()));
    }

    [Fact]
    public void Rotation_atomically_fences_old_credential()
    {
        var lifecycle = Lifecycle();
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated,
            lifecycle.Rotate("operator-a", "cred-v2", "fp-v2", KeyV2));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialSequenceMismatch,
            lifecycle.Authorize(Credential()));
        Assert.Equal(("cred-v2", "fp-v2", 8L, AuthorizationRecoveryRepairProposerCredentialState.Active),
            lifecycle.Snapshot("operator-a"));
    }

    [Fact]
    public void New_credential_works_after_rotation()
    {
        var lifecycle = Lifecycle();
        lifecycle.Rotate("operator-a", "cred-v2", "fp-v2", KeyV2);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized,
            lifecycle.Authorize(Credential("cred-v2", "fp-v2", 8)));
    }

    [Fact]
    public void Revocation_fences_in_flight_credentials()
    {
        var lifecycle = Lifecycle();
        lifecycle.Revoke("operator-a");
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialNotActive,
            lifecycle.Authorize(Credential()));
    }

    [Fact]
    public void Retired_credential_fails_closed()
    {
        var lifecycle = Lifecycle();
        lifecycle.Retire("operator-a");
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialNotActive,
            lifecycle.Authorize(Credential()));
    }

    [Fact]
    public void Old_proof_cannot_authorize_new_sequence()
    {
        var lifecycle = Lifecycle();
        lifecycle.Rotate("operator-a", "cred-v2", "fp-v2", KeyV2);
        var old = Credential();
        var forged = old with { CredentialId = "cred-v2", CredentialFingerprint = "fp-v2", CredentialSequence = 8 };
        Assert.NotEqual(AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized,
            lifecycle.Authorize(forged));
    }

    [Fact]
    public void Rotation_and_revocation_are_serialized_with_authorization()
    {
        var lifecycle = Lifecycle();
        var outcomes = new ConcurrentBag<AuthorizationRecoveryRepairProposerCredentialAttemptResult>();
        Parallel.For(0, 32, i =>
        {
            if ((i & 1) == 0)
                outcomes.Add(lifecycle.Authorize(Credential(tx: $"repair-{i}-21")));
            else
                lifecycle.Revoke("operator-a");
        });

        Assert.All(outcomes, result =>
            Assert.Contains(result, new[]
            {
                AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized,
                AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialNotActive
            }));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialState.Revoked, lifecycle.Snapshot("operator-a").State);
    }

    [Fact]
    public void Repeated_revocation_is_idempotent()
    {
        var lifecycle = Lifecycle();
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated, lifecycle.Revoke("operator-a"));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked, lifecycle.Revoke("operator-a"));
    }
}
