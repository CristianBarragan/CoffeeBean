using Foundgine.Execution;

namespace Foundgine.Authorization;

/// <summary>
/// capability scope binds authorization evidence to the exact capability
/// contract and permitted effect set. A valid authorization decision for one
/// consequential operation cannot be confused with another operation merely
/// because actor, tenant, resources, and authorization version match.
/// </summary>
public sealed record AuthorizationCapabilityScope(
 string CapabilityId,
 int CapabilityVersion,
 string Operation,
 IReadOnlyList<string> AllowedEffects,
 string ScopeFingerprint)
{
 public static AuthorizationCapabilityScope Create(
 string capabilityId,
 int capabilityVersion,
 string operation,
 IEnumerable<string> allowedEffects)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
 ArgumentException.ThrowIfNullOrWhiteSpace(operation);
 if (capabilityVersion <= 0) throw new ArgumentOutOfRangeException(nameof(capabilityVersion));

 var effects = allowedEffects
 .Where(static x => !string.IsNullOrWhiteSpace(x))
 .Select(static x => x.Trim())
 .Distinct(StringComparer.Ordinal)
 .OrderBy(static x => x, StringComparer.Ordinal)
 .ToArray();

 if (effects.Length == 0)
 throw new InvalidOperationException("At least one authorization effect is required.");

 var canonical = string.Join("|", new[]
 {
 "foundgine.authorization-capability-scope.v1",
 capabilityId,
 capabilityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
 operation,
 string.Join(",", effects)
 });

 return new(capabilityId, capabilityVersion, operation, effects,
 ExecutionEvidenceFactory.Hash(canonical));
 }

 public void Require(string capabilityId, int capabilityVersion, string operation, string effect)
 {
 if (!string.Equals(CapabilityId, capabilityId, StringComparison.Ordinal) ||
 CapabilityVersion != capabilityVersion ||
 !string.Equals(Operation, operation, StringComparison.Ordinal) ||
 !AllowedEffects.Contains(effect, StringComparer.Ordinal))
 {
 throw new InvalidOperationException(
 "Authorization capability scope does not permit the requested operation or effect; authorization fails closed.");
 }

 var expected = Create(CapabilityId, CapabilityVersion, Operation, AllowedEffects);
 if (!string.Equals(ScopeFingerprint, expected.ScopeFingerprint, StringComparison.Ordinal))
 throw new InvalidOperationException("Authorization capability scope integrity verification failed.");
 }
}
