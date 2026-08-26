using System.Security.Cryptography;
using Foundgine.Semantics.Security.Warrants;
using Xunit;

namespace Foundgine.Security.Tests.Penetration;

/// <summary>
/// PT-25 / PT-26 / PT-27: attacks against the warrant trust boundary itself,
/// as distinct from attacks against a request already carrying a validated
/// warrant. These tests document configuration-dependent gaps: they fail
/// (i.e. an attack that should be rejected is instead accepted) whenever the
/// host application does not actively close them, because the primitives
/// involved are opt-in rather than fail-closed by default.
/// </summary>
public sealed class WarrantTrustBoundaryPenetrationTests
{
    // ------------------------------------------------------------------
    // PT-25: issuer trust is opt-in. SecurityWarrantVerifier.Verify only
    // checks Issuer when the caller supplies a non-null expectedIssuer. A
    // host that forgets to configure FoundgineOptions.ExpectedWarrantIssuer
    // will accept a warrant from *any* issuer whose KeyId resolves to a key
    // the resolver happens to trust.
    // ------------------------------------------------------------------

    [Fact]
    public void Forged_issuer_is_rejected_when_expected_issuer_is_configured()
    {
        using var key = RSA.Create(2048);
        var forged = Sign(Create(DateTimeOffset.UtcNow, issuer: "attacker-controlled-issuer"), key);

        // A correctly configured host supplies the trusted root issuer.
        Assert.Throws<InvalidOperationException>(() =>
            SecurityWarrantVerifier.Verify(
                forged,
                new Resolver(forged.KeyId, key),
                DateTimeOffset.UtcNow,
                expectedIssuer: "trusted-root-issuer"));
    }

    [Fact]
    public void ATTACK_forged_issuer_is_accepted_when_expected_issuer_is_left_unconfigured()
    {
        // Simulates a host that leaves FoundgineOptions.ExpectedWarrantIssuer
        // null (its documented default). Any warrant signed by any key the
        // resolver can resolve is accepted, regardless of Issuer content -
        // Issuer is an attacker-supplied field, not a trust anchor by itself.
        using var key = RSA.Create(2048);
        var forged = Sign(Create(DateTimeOffset.UtcNow, issuer: "attacker-controlled-issuer"), key);

        var exception = Record.Exception(() =>
            SecurityWarrantVerifier.Verify(
                forged,
                new Resolver(forged.KeyId, key),
                DateTimeOffset.UtcNow,
                expectedIssuer: null));

        // This currently succeeds (exception is null): the "attack" test
        // passes today, which is the point - it demonstrates the gap rather
        // than proving the framework enforces a trust anchor unconditionally.
        // If SecurityWarrantVerifier.Verify is later made to require a
        // non-null expectedIssuer whenever a warrant is presented, this
        // assertion should be flipped to Assert.NotNull(exception).
        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // PT-26: delegation-chain trust (SecurityWarrantDelegationChainValidator,
    // SecurityWarrantDelegationTrust) is fully implemented and unit-tested in
    // isolation, but is never invoked by FoundgineEngine / FoundgineMutationEngine.
    // A warrant carrying delegation ancestry (ParentId/ParentDigest/DelegationPath)
    // is verified by SecurityWarrantVerifier.Verify exactly like a root warrant:
    // its own signature and its own Issuer/Audience/time bounds are checked, but
    // nothing walks the chain, checks issuer delegation authority, or enforces
    // depth/attenuation. If a caller can obtain any single signature over a
    // warrant object that merely has delegation-shaped fields populated, that
    // signature alone is sufficient - the ancestry fields are decorative unless
    // the host separately calls the chain validator itself.
    // ------------------------------------------------------------------

    [Fact]
    public void ATTACK_single_signature_verification_ignores_delegation_ancestry_entirely()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;

        // A "child" warrant whose ParentId/ParentDigest/DelegationPath point at
        // a parent that was never verified, never had delegation authority
        // checked, and does not need to exist at all for this call to succeed.
        var uncheckedChild = Sign(
            Create(now, issuer: "root-issuer") with
            {
                Id = "child-1",
                ParentId = "never-verified-parent",
                ParentDigest = "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                DelegationPath = ["0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"],
            },
            key);

        // Verify() succeeds: it only checks this one warrant's own signature,
        // issuer string and time bounds - it never calls
        // SecurityWarrantDelegationChainValidator.Validate or
        // SecurityWarrantDelegationTrust.VerifyIssuer.
        var exception = Record.Exception(() =>
            SecurityWarrantVerifier.Verify(
                uncheckedChild,
                new Resolver(uncheckedChild.KeyId, key),
                now,
                expectedIssuer: "root-issuer"));

        Assert.Null(exception);

        // A host that believes delegation depth/attenuation/trust is enforced
        // by virtue of these fields being present is mistaken: nothing in the
        // execution path calls the chain validator that would have rejected
        // this (e.g. because the referenced parent digest is fabricated).
    }

    // ------------------------------------------------------------------
    // PT-27: replay protection is process-local. MemorySecurityWarrantReplayStore
    // has no cross-instance or cross-restart durability. This test does not
    // "break" single-instance replay protection (that already works - see
    // SecurityWarrantTests.Same_warrant_same_nonce_can_only_be_consumed_once);
    // it demonstrates that a second, independent store instance - standing in
    // for a second process/pod/replica - has no visibility into consumption
    // recorded by the first, so the same warrant is replayable once per
    // uncoordinated store.
    // ------------------------------------------------------------------

    [Fact]
    public void ATTACK_replay_guard_does_not_share_state_across_store_instances()
    {
        var w = Create(DateTimeOffset.UtcNow);

        var instanceA = new MemorySecurityWarrantReplayStore();
        var instanceB = new MemorySecurityWarrantReplayStore();

        // Consumed once on "instance A" (e.g. one replica behind a load balancer).
        SecurityWarrantReplayGuard.Consume(w, instanceA, DateTimeOffset.UtcNow);

        // The identical warrant, replayed against an independent, uncoordinated
        // "instance B", is accepted again - there is nothing in the shipped
        // MemorySecurityWarrantReplayStore that makes this fail.
        var exception = Record.Exception(() =>
            SecurityWarrantReplayGuard.Consume(w, instanceB, DateTimeOffset.UtcNow));

        Assert.Null(exception);

        // A horizontally scaled deployment using the default in-memory store
        // (rather than a shared/distributed store) effectively grants one
        // replay per replica for every warrant, for the lifetime of the
        // warrant's validity window.
    }

    private static SecurityWarrant Create(
        DateTimeOffset now,
        string issuer = "issuer",
        string audience = "foundgine") => new(
        "warrant-1", issuer, "agent-a", audience,
        [new CapabilityGrant("Customer.read", "read", ["customer/*"])],
        new SecurityWarrantConstraints(allowedTenants: ["tenant-1"], maxResults: 100, maxAmount: 1000m),
        now.AddMinutes(-1), now.AddHours(1), "nonce-1", "key-1", null, []);

    private static SecurityWarrant Sign(SecurityWarrant warrant, RSA key) =>
        SecurityWarrantSigner.Sign(warrant, key);

    private sealed class Resolver(string id, RSA key) : ISecurityWarrantKeyResolver
    {
        public RSA Resolve(string keyId) =>
            StringComparer.Ordinal.Equals(id, keyId) ? key : throw new InvalidOperationException("Unknown key");
    }
}
