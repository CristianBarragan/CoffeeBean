namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>Current persisted authorization state used to invalidate prior evidence.</summary>
public sealed record AuthorizationEvidenceAuthorityState(
    Guid ActorId,
    int TenantId,
    bool Allowed,
    long Version,
    string Fingerprint);

/// <summary>
/// M5.23 transition guard. Fresh, cryptographically valid evidence is still rejected
/// when the persisted authorization authority has moved past the evidence version or
/// has been revoked. The comparison is intentionally execution-time state.
/// </summary>
public static class AuthorizationEvidenceTransitionGuard
{
    public static void Validate(
        AuthorizationEvidenceTemporalClaims evidence,
        AuthorizationEvidenceAuthorityState current)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(current);

        if (current.ActorId != evidence.ActorId || current.TenantId != evidence.TenantId)
            throw new InvalidOperationException("Authorization evidence identity does not match current authority; authorization fails closed.");

        if (!current.Allowed)
            throw new UnauthorizedAccessException("Authorization authority has been revoked; prior evidence is invalid.");

        if (current.Version != evidence.AuthorizationVersion)
            throw new InvalidOperationException(
                $"Authorization evidence version is no longer current. Evidence={evidence.AuthorizationVersion}, current={current.Version}; authorization fails closed.");

        if (string.IsNullOrWhiteSpace(current.Fingerprint))
            throw new InvalidOperationException("Current authorization authority fingerprint is missing; authorization fails closed.");
    }
}

/// <summary>Explicit transition result for administrative authorization changes.</summary>
public enum AuthorizationEvidenceTransition
{
    Grant = 1,
    Update = 2,
    Revoke = 3
}

/// <summary>
/// Monotonic transition validator used before a persistence-layer UPDATE/DELETE.
/// PostgreSQL callers must hold the authorization_context row lock while applying
/// the transition so concurrent readers cannot validate against a state that has
/// already been superseded in the same transaction boundary.
/// </summary>
public static class AuthorizationAuthorityTransitionValidator
{
    public static void ValidateNextVersion(
        AuthorizationEvidenceAuthorityState current,
        bool nextAllowed,
        long nextVersion,
        string nextFingerprint)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (nextVersion <= current.Version)
            throw new InvalidOperationException(
                $"Authorization transition version must increase monotonically. Current={current.Version}, requested={nextVersion}.");
        if (string.IsNullOrWhiteSpace(nextFingerprint))
            throw new InvalidOperationException("Authorization transition fingerprint is required.");

        // Re-granting after revocation is allowed only as a strictly newer authority version.
        // The old version therefore cannot become valid again merely because its evidence
        // remains within its cryptographic/freshness lifetime.
        _ = nextAllowed;
    }
}
