using System.Collections.Concurrent;
using System.Security.Cryptography;
using Foundgine.Runtime.ControlPlane;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairProposerAuthenticationTests
{
    private static readonly byte[] Key = SHA256.HashData("m5.69-test-key"u8.ToArray());

    private static AuthorizationRecoveryControlPlaneRepairProposerAuthentication Auth()
    {
        var auth = new AuthorizationRecoveryControlPlaneRepairProposerAuthentication();
        auth.Register("operator-a", "fp-a", Key, 7);
        return auth;
    }

    private static AuthorizationRecoveryRepairProposerCredential Credential(
        string tx = "repair-20-21",
        string expectedFp = "fp20",
        string targetFp = "fp21",
        string expectedHead = "h20",
        string targetHead = "h21",
        string proposer = "operator-a",
        long sequence = 7,
        string fingerprint = "fp-a")
    {
        var c = new AuthorizationRecoveryRepairProposerCredential(
            proposer, "cred-7", sequence, fingerprint, tx, 20, expectedFp, expectedHead,
            21, targetFp, targetHead, "");
        return c with { Proof = AuthorizationRecoveryControlPlaneRepairProposerAuthentication.CreateProof(c, Key) };
    }

    [Fact]
    public void Exact_transaction_and_state_binding_is_authorized()
    {
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.Authorized,
            Auth().Authorize(Credential()));
    }

    [Fact]
    public void Unknown_proposer_fails_closed()
    {
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.UnknownProposer,
            Auth().Authorize(Credential(proposer: "attacker")));
    }

    [Fact]
    public void Retired_proposer_fails_closed()
    {
        var auth = Auth();
        auth.SetState("operator-a", AuthorizationRecoveryRepairProposerCredentialState.Retired);
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialNotActive,
            auth.Authorize(Credential()));
    }

    [Fact]
    public void Credential_cannot_cross_transaction_identity()
    {
        var credential = Credential();
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.ProofMismatch,
            Auth().Authorize(credential with { TransactionId = "repair-99-100" }));
    }

    [Fact]
    public void Credential_cannot_cross_durable_state()
    {
        var credential = Credential();
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.ProofMismatch,
            Auth().Authorize(credential with { ExpectedStateFingerprint = "evil" }));
    }

    [Fact]
    public void Credential_sequence_is_fenced()
    {
        var auth = Auth();
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialSequenceMismatch,
            auth.Authorize(Credential(sequence: 6)));
    }

    [Fact]
    public void Fingerprint_forgery_fails_closed()
    {
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialFingerprintMismatch,
            Auth().Authorize(Credential(fingerprint: "forged")));
    }

    [Fact]
    public void Proof_from_another_key_fails_closed()
    {
        var credential = Credential();
        var other = SHA256.HashData("other-key"u8.ToArray());
        var forged = credential with { Proof = AuthorizationRecoveryControlPlaneRepairProposerAuthentication.CreateProof(credential, other) };
        Assert.Equal(AuthorizationRecoveryRepairProposerAuthorizationResult.ProofMismatch, Auth().Authorize(forged));
    }

    [Fact]
    public void Thirty_two_concurrent_authorizations_are_all_independently_bound()
    {
        var auth = Auth();
        var results = new ConcurrentBag<AuthorizationRecoveryRepairProposerAuthorizationResult>();
        Parallel.For(0, 32, i => results.Add(auth.Authorize(Credential(tx: $"repair-{i}-21"))));
        Assert.Equal(32, results.Count(x => x == AuthorizationRecoveryRepairProposerAuthorizationResult.Authorized));
    }
}
