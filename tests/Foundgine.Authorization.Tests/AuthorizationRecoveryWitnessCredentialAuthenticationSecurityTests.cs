using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryWitnessCredentialAuthenticationSecurityTests
{
    [Fact]
    public void Matching_identity_and_fingerprint_are_accepted()
    {
        var verifier = new FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
            new Dictionary<string, string> { ["w1"] = "fp-1" });

        Assert.True(verifier.Authenticate("w1", new AuthorizationRecoveryWitnessCredential("w1", "fp-1")));
    }

    [Fact]
    public void Same_witness_id_with_forged_fingerprint_is_rejected()
    {
        var verifier = new FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
            new Dictionary<string, string> { ["w1"] = "fp-1" });

        Assert.False(verifier.Authenticate("w1", new AuthorizationRecoveryWitnessCredential("w1", "attacker")));
    }

    [Fact]
    public void Credential_for_one_witness_cannot_authenticate_another()
    {
        var verifier = new FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
            new Dictionary<string, string> { ["w1"] = "fp-1", ["w2"] = "fp-2" });

        Assert.False(verifier.Authenticate("w2", new AuthorizationRecoveryWitnessCredential("w1", "fp-1")));
    }

    [Fact]
    public void Unsupported_credential_version_is_rejected()
    {
        var verifier = new FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
            new Dictionary<string, string> { ["w1"] = "fp-1" });

        Assert.False(verifier.Authenticate("w1", new AuthorizationRecoveryWitnessCredential("w1", "fp-1", "v2")));
    }

    [Fact]
    public void Unknown_witness_is_rejected_even_with_a_valid_looking_fingerprint()
    {
        var verifier = new FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
            new Dictionary<string, string> { ["w1"] = "fp-1" });

        Assert.False(verifier.Authenticate("attacker", new AuthorizationRecoveryWitnessCredential("attacker", "fp-1")));
    }
}
