using System.Security.Cryptography;
using System.Text;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.HighAssurance.Postgres.Tests;

public sealed class AuthorizationRecoveryAuthorityTermQuorumAttestationSecurityTests
{
    private static byte[] Key(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static (AuthorizationRecoveryAuthorityTermCertificate Certificate, AuthorizationRecoveryAuthorityTermQuorumVerifier Verifier, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Keys) Setup()
    {
        var predecessor = Key("authority-A");
        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            1, "authority-A", 2, "authority-B", new string('a', 64), "key-A", predecessor);
        var keys = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            ["w1"] = Key("w1"), ["w2"] = Key("w2"), ["w3"] = Key("w3")
        };
        return (certificate, new AuthorizationRecoveryAuthorityTermQuorumVerifier(keys.Keys.ToArray(), keys), keys);
    }

    [Fact]
    public void Majority_of_independent_witnesses_accepts_certificate()
    {
        var (certificate, verifier, keys) = Setup();
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, keys, ["w1", "w2"]);
        Assert.True(verifier.Verify(certificate, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }

    [Fact]
    public void Minority_attestation_is_rejected()
    {
        var (certificate, verifier, keys) = Setup();
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, keys, ["w1"]);
        Assert.False(verifier.Verify(certificate, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }

    [Fact]
    public void Attestation_from_unknown_witness_does_not_count()
    {
        var (certificate, verifier, keys) = Setup();
        var expanded = new Dictionary<string, ReadOnlyMemory<byte>>(keys) { ["evil"] = Key("evil") };
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, expanded, ["evil", "w1"]);
        Assert.False(verifier.Verify(certificate, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }

    [Fact]
    public void Tampered_certificate_digest_invalidates_all_witness_attestations()
    {
        var (certificate, verifier, keys) = Setup();
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, keys, ["w1", "w2"]);
        var tampered = certificate with { NewAuthorityId = "attacker" };
        Assert.False(verifier.Verify(tampered, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }

    [Fact]
    public void Wrong_witness_key_cannot_satisfy_quorum()
    {
        var (certificate, verifier, keys) = Setup();
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, keys, ["w1", "w2"]);
        var wrong = new Dictionary<string, ReadOnlyMemory<byte>>(keys) { ["w2"] = Key("attacker") };
        var wrongVerifier = new AuthorizationRecoveryAuthorityTermQuorumVerifier(wrong.Keys.ToArray(), wrong);
        Assert.False(wrongVerifier.Verify(certificate, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }

    [Fact]
    public void Duplicate_witness_identity_is_rejected()
    {
        var (certificate, verifier, keys) = Setup();
        var attestation = AuthorizationRecoveryAuthorityTermQuorumAttestation.Create(certificate, keys, ["w1", "w1", "w2"]);
        Assert.Equal(2, attestation.WitnessSignatures.Count);
        Assert.True(verifier.Verify(certificate, attestation, new AuthorizationRecoveryAuthorityState(1, "authority-A"), Key("authority-A")));
    }
}
