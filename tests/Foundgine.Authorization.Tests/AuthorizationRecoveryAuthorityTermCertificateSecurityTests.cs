using System.Security.Cryptography;
using System.Text;
using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryAuthorityTermCertificateSecurityTests
{
    private static readonly byte[] KeyA = SHA256.HashData(Encoding.UTF8.GetBytes("authority-A-key"));
    private static readonly byte[] WrongKey = SHA256.HashData(Encoding.UTF8.GetBytes("wrong-key"));

    [Fact]
    public async Task Current_authority_can_issue_a_direct_successor_certificate()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var digest = await anchor.ReadAuthorityCertificateDigestAsync();

        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 2, "authority-B", digest, "key-A", KeyA);

        Assert.True(certificate.Verify(KeyA, current));
        Assert.True(await anchor.TryInstallAuthorityCertificateAsync(certificate, KeyA));

        var installed = await anchor.ReadAuthorityAsync();
        Assert.Equal(2, installed.Term);
        Assert.Equal("authority-B", installed.AuthorityId);
    }

    [Fact]
    public async Task Wrong_signing_key_cannot_install_a_term()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var digest = await anchor.ReadAuthorityCertificateDigestAsync();
        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 2, "authority-B", digest, "key-A", KeyA);

        Assert.False(await anchor.TryInstallAuthorityCertificateAsync(certificate, WrongKey));
        Assert.Equal(1, (await anchor.ReadAuthorityAsync()).Term);
    }

    [Fact]
    public async Task Term_skip_is_rejected_even_with_a_valid_signature()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var digest = await anchor.ReadAuthorityCertificateDigestAsync();

        Assert.Throws<ArgumentException>(() => AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 3, "authority-C", digest, "key-A", KeyA));
    }

    [Fact]
    public async Task Certificate_from_a_different_history_is_rejected()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 2, "authority-B", new string('f', 64), "key-A", KeyA);

        Assert.False(await anchor.TryInstallAuthorityCertificateAsync(certificate, KeyA));
        Assert.Equal(1, (await anchor.ReadAuthorityAsync()).Term);
    }

    [Fact]
    public async Task Tampering_with_successor_identity_invalidates_the_signature()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var digest = await anchor.ReadAuthorityCertificateDigestAsync();
        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 2, "authority-B", digest, "key-A", KeyA);
        var tampered = certificate with { NewAuthorityId = "authority-attacker" };

        Assert.False(tampered.Verify(KeyA, current));
        Assert.False(await anchor.TryInstallAuthorityCertificateAsync(tampered, KeyA));
    }

    [Fact]
    public async Task Replaying_the_same_certificate_after_install_is_idempotently_rejected()
    {
        var anchor = new InMemoryAuthorizationRecoveryAuthorityAnchor("authority-A");
        var current = await anchor.ReadAuthorityAsync();
        var digest = await anchor.ReadAuthorityCertificateDigestAsync();
        var certificate = AuthorizationRecoveryAuthorityTermCertificate.Create(
            current.Term, current.AuthorityId, 2, "authority-B", digest, "key-A", KeyA);

        Assert.True(await anchor.TryInstallAuthorityCertificateAsync(certificate, KeyA));
        Assert.False(await anchor.TryInstallAuthorityCertificateAsync(certificate, KeyA));
        Assert.Equal(2, (await anchor.ReadAuthorityAsync()).Term);
    }
}
