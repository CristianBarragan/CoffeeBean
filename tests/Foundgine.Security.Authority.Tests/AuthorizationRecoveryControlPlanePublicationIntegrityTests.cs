using System.Text;
using Foundgine.Runtime.ControlPlane;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePublicationIntegrityTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("test-integrity-key-v1");

    private static AuthorizationRecoveryControlPlanePublication Create()
    {
        const long epoch = 8;
        const string owner = "secondary";
        const long sequence = 42;
        const string digest = "digest-A";
        const string keyId = "key-v1";

        var tag = AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
            epoch, owner, sequence, digest, keyId, Key);

        return new AuthorizationRecoveryControlPlanePublication(
            epoch, owner, sequence, digest, keyId,
            AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
            tag);
    }

    [Fact]
    public void Untampered_publication_verifies()
    {
        Assert.True(
            AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(Create(), Key));
    }

    [Fact]
    public void Owner_tampering_fails()
    {
        var p = Create() with { ActiveControlPlaneId = "attacker" };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void Epoch_tampering_fails()
    {
        var p = Create() with { Epoch = 9 };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void Sequence_tampering_fails()
    {
        var p = Create() with { Sequence = 41 };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void History_digest_tampering_fails()
    {
        var p = Create() with { HeadDigest = "digest-B" };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void Key_id_tampering_fails()
    {
        var p = Create() with { IntegrityKeyId = "key-v2" };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void Algorithm_confusion_fails()
    {
        var p = Create() with { AlgorithmVersion = "none" };
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(p, Key));
    }

    [Fact]
    public void Wrong_key_fails()
    {
        var wrong = Encoding.UTF8.GetBytes("wrong-key");
        Assert.False(AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(Create(), wrong));
    }

    [Fact]
    public void Canonicalization_binds_field_boundaries()
    {
        var a = Create();

        // Shift the owner/sequence boundary in a way that would collide under
        // naive (non-length-prefixed) concatenation: "secondary" + "42" vs
        // "secondary4" + "2" both flatten to "secondary42" without the
        // length-prefix that Canonicalize adds per field.
        var bTag = AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
            a.Epoch, "secondary4", 2, a.HeadDigest, a.IntegrityKeyId, Key);

        Assert.NotEqual(a.Tag, bTag);
    }
}