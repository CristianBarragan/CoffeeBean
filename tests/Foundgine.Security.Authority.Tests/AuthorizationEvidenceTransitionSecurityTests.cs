using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

/// <summary>adversarial tests for revocation and authorization-state transitions.</summary>
public sealed class AuthorizationEvidenceTransitionSecurityTests
{
 private static readonly DateTimeOffset Now = new(2026, 8, 21, 7, 0, 0, TimeSpan.Zero);
 private static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");

 private static AuthorizationEvidenceTemporalClaims Evidence(long version = 7) =>
 new(Actor, 7, version, Now.AddMinutes(-1), Now.AddMinutes(4));

 [Fact]
 public void Current_allowed_same_version_is_accepted()
 {
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(),
 new AuthorizationEvidenceAuthorityState(Actor, 7, true, 7, "fp-7"));
 }

 [Fact]
 public void Revocation_invalidates_still_fresh_evidence()
 {
 var ex = Assert.Throws<UnauthorizedAccessException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(),
 new AuthorizationEvidenceAuthorityState(Actor, 7, false, 8, "fp-8")));

 Assert.Contains("revoked", ex.Message, StringComparison.OrdinalIgnoreCase);
 }

 [Fact]
 public void Newer_authorization_version_invalidates_old_evidence()
 {
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(7),
 new AuthorizationEvidenceAuthorityState(Actor, 7, true, 8, "fp-8")));
 }

 [Fact]
 public void Older_authority_version_cannot_authorize_newer_evidence()
 {
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(8),
 new AuthorizationEvidenceAuthorityState(Actor, 7, true, 7, "fp-7")));
 }

 [Fact]
 public void Cross_actor_transition_fails_closed()
 {
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(),
 new AuthorizationEvidenceAuthorityState(
 Guid.Parse("22222222-2222-2222-2222-222222222222"), 7, true, 7, "fp-7")));
 }

 [Fact]
 public void Cross_tenant_transition_fails_closed()
 {
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(
 Evidence(),
 new AuthorizationEvidenceAuthorityState(Actor, 8, true, 7, "fp-7")));
 }

 [Fact]
 public void Transition_version_must_increase()
 {
 var current = new AuthorizationEvidenceAuthorityState(Actor, 7, true, 7, "fp-7");

 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationAuthorityTransitionValidator.ValidateNextVersion(current, false, 7, "fp-7"));
 Assert.Throws<InvalidOperationException>(() =>
 AuthorizationAuthorityTransitionValidator.ValidateNextVersion(current, true, 6, "fp-6"));
 }

 [Fact]
 public void Revocation_then_regrant_requires_a_new_version()
 {
 var revoked = new AuthorizationEvidenceAuthorityState(Actor, 7, false, 8, "fp-8");

 Assert.Throws<UnauthorizedAccessException>(() =>
 AuthorizationEvidenceTransitionGuard.Validate(Evidence(7), revoked));

 AuthorizationAuthorityTransitionValidator.ValidateNextVersion(revoked, true, 9, "fp-9");
 }
}
