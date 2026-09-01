using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

/// <summary>adversarial temporal authorization tests; no PostgreSQL required.</summary>
public sealed class AuthorizationEvidenceFreshnessSecurityTests
{
 private static readonly DateTimeOffset Now = new(2026, 8, 21, 7, 0, 0, TimeSpan.Zero);

 private static AuthorizationEvidenceTemporalClaims Claims(
 DateTimeOffset issued, DateTimeOffset expires,
 Guid? actor = null, int tenant = 7, long version = 3) =>
 new(actor ?? Guid.Parse("11111111-1111-1111-1111-111111111111"), tenant, version, issued, expires);

 [Fact]
 public void Fresh_evidence_is_accepted()
 {
 var validator = new AuthorizationEvidenceFreshnessValidator(
 new AuthorizationEvidenceFreshnessPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30)),
 () => Now);

 validator.Validate(Claims(Now.AddMinutes(-1), Now.AddMinutes(4)));
 }

 [Fact]
 public void Expired_evidence_fails_closed()
 {
 var validator = new AuthorizationEvidenceFreshnessValidator(
 AuthorizationEvidenceFreshnessPolicy.Default, () => Now);

 // issued -6min/expires -1min keeps the lifetime at exactly the 5-minute
 // policy bound (so that check doesn't fire first) while the evidence is
 // still genuinely past its expiry, which is what this test targets.
 var ex = Assert.Throws<InvalidOperationException>(() =>
 validator.Validate(Claims(Now.AddMinutes(-6), Now.AddMinutes(-1))));

 Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
 }

 [Fact]
 public void Future_dated_evidence_beyond_clock_skew_fails_closed()
 {
 var validator = new AuthorizationEvidenceFreshnessValidator(
 AuthorizationEvidenceFreshnessPolicy.Default, () => Now);

 Assert.Throws<InvalidOperationException>(() =>
 validator.Validate(Claims(Now.AddMinutes(1), Now.AddMinutes(2))));
 }

 [Fact]
 public void Excessive_lifetime_is_rejected_even_before_expiration()
 {
 var validator = new AuthorizationEvidenceFreshnessValidator(
 AuthorizationEvidenceFreshnessPolicy.Default, () => Now);

 Assert.Throws<InvalidOperationException>(() =>
 validator.Validate(Claims(Now.AddMinutes(-1), Now.AddMinutes(20))));
 }

 [Fact]
 public void Temporal_binding_cannot_be_replayed_for_another_actor()
 {
 var key = new byte[32];
 var first = Claims(Now.AddMinutes(-1), Now.AddMinutes(4));
 var tag = AuthorizationEvidenceTemporalBinding.ComputeBinding(key, first);
 var replay = first with { ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222") };

 Assert.False(AuthorizationEvidenceTemporalBinding.VerifyBinding(key, replay, tag));
 }

 [Fact]
 public void Temporal_binding_cannot_be_replayed_for_another_version()
 {
 var key = new byte[32];
 var first = Claims(Now.AddMinutes(-1), Now.AddMinutes(4));
 var tag = AuthorizationEvidenceTemporalBinding.ComputeBinding(key, first);
 var replay = first with { AuthorizationVersion = first.AuthorizationVersion + 1 };

 Assert.False(AuthorizationEvidenceTemporalBinding.VerifyBinding(key, replay, tag));
 }

 [Fact]
 public void Temporal_binding_cannot_be_replayed_after_expiration_change()
 {
 var key = new byte[32];
 var first = Claims(Now.AddMinutes(-1), Now.AddMinutes(4));
 var tag = AuthorizationEvidenceTemporalBinding.ComputeBinding(key, first);
 var replay = first with { ExpiresAtUtc = Now.AddHours(1) };

 Assert.False(AuthorizationEvidenceTemporalBinding.VerifyBinding(key, replay, tag));
 }

 [Fact]
 public void Malformed_temporal_window_fails_closed()
 {
 var validator = new AuthorizationEvidenceFreshnessValidator(
 AuthorizationEvidenceFreshnessPolicy.Default, () => Now);

 Assert.Throws<InvalidOperationException>(() =>
 validator.Validate(Claims(Now, Now)));
 }
}