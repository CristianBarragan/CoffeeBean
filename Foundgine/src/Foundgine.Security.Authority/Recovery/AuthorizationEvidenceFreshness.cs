using System.Security.Cryptography;

namespace Foundgine.Security.Authority;

/// <summary>temporal policy for authorization evidence.</summary>
public sealed record AuthorizationEvidenceFreshnessPolicy
{
 public TimeSpan MaximumAge { get; }
 public TimeSpan MaximumLifetime { get; }
 public TimeSpan AllowedClockSkew { get; }

 public static AuthorizationEvidenceFreshnessPolicy Default { get; } =
 new(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));

 public AuthorizationEvidenceFreshnessPolicy(TimeSpan maximumAge, TimeSpan maximumLifetime, TimeSpan allowedClockSkew)
 {
 MaximumAge = maximumAge;
 MaximumLifetime = maximumLifetime;
 AllowedClockSkew = allowedClockSkew;
 if (MaximumAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumAge));
 if (MaximumLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumLifetime));
 if (AllowedClockSkew < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(AllowedClockSkew));
 if (MaximumLifetime < MaximumAge)
 throw new ArgumentException("Maximum lifetime must be at least maximum age.");
 }
}

/// <summary>Signed temporal claims bound to the authorization identity.</summary>
public sealed record AuthorizationEvidenceTemporalClaims(
 Guid ActorId,
 int TenantId,
 long AuthorizationVersion,
 DateTimeOffset IssuedAtUtc,
 DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Fail-closed validation of authorization evidence freshness. The clock is injected
/// so tests and production can use an explicit trusted time source rather than hiding
/// a system-clock dependency inside authorization logic.
/// </summary>
public sealed class AuthorizationEvidenceFreshnessValidator
{
 private readonly AuthorizationEvidenceFreshnessPolicy _policy;
 private readonly Func<DateTimeOffset> _utcNow;

 public AuthorizationEvidenceFreshnessValidator(
 AuthorizationEvidenceFreshnessPolicy? policy = null,
 Func<DateTimeOffset>? utcNow = null)
 {
 _policy = policy ?? AuthorizationEvidenceFreshnessPolicy.Default;
 _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
 }

 public void Validate(AuthorizationEvidenceTemporalClaims claims)
 {
 if (claims.ActorId == Guid.Empty)
 throw new InvalidOperationException("Authorization evidence actor identity is missing.");
 if (claims.TenantId <= 0)
 throw new InvalidOperationException("Authorization evidence tenant identity is invalid.");
 if (claims.AuthorizationVersion <= 0)
 throw new InvalidOperationException("Authorization evidence version is invalid.");

 var now = _utcNow();
 if (claims.IssuedAtUtc > now + _policy.AllowedClockSkew)
 throw new InvalidOperationException("Authorization evidence is not yet valid; authorization fails closed.");

 if (claims.ExpiresAtUtc <= claims.IssuedAtUtc)
 throw new InvalidOperationException("Authorization evidence expiration must be after issuance.");

 if (claims.ExpiresAtUtc - claims.IssuedAtUtc > _policy.MaximumLifetime)
 throw new InvalidOperationException("Authorization evidence lifetime exceeds the configured security bound.");

 if (now > claims.ExpiresAtUtc + _policy.AllowedClockSkew)
 throw new InvalidOperationException("Authorization evidence has expired; authorization fails closed.");

 if (now - claims.IssuedAtUtc > _policy.MaximumAge + _policy.AllowedClockSkew)
 throw new InvalidOperationException("Authorization evidence is stale; authorization fails closed.");
 }
}

/// <summary>
/// Binds temporal claims to the same identity/version tuple already authenticated by
/// . A valid tag from one temporal context cannot be replayed for another.
/// </summary>
public static class AuthorizationEvidenceTemporalBinding
{
 public static byte[] ComputeBinding(
 byte[] integrityKey,
 AuthorizationEvidenceTemporalClaims claims)
 {
 ArgumentNullException.ThrowIfNull(integrityKey);
 ArgumentNullException.ThrowIfNull(claims);
 if (integrityKey.Length == 0) throw new ArgumentException("Integrity key is required.", nameof(integrityKey));

 var canonical = string.Join("|",
 Encode("authorization-temporal-v1"),
 Encode(claims.ActorId.ToString("D")),
 Encode(claims.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
 Encode(claims.AuthorizationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
 Encode(claims.IssuedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
 Encode(claims.ExpiresAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

 return HMACSHA256.HashData(integrityKey, System.Text.Encoding.UTF8.GetBytes(canonical));
 }

 public static bool VerifyBinding(
 byte[] integrityKey,
 AuthorizationEvidenceTemporalClaims claims,
 ReadOnlySpan<byte> suppliedTag)
 {
 var expected = ComputeBinding(integrityKey, claims);
 return CryptographicOperations.FixedTimeEquals(expected, suppliedTag);
 }

 private static string Encode(string value) => $"{value.Length}:{value}";
}
