using System.Globalization;
using System.Text.RegularExpressions;

namespace Foundgine.SupplyChain.Semantic.Authorization;

/// <summary>
/// A single rejected client claim, kept for audit/diagnostic purposes. The
/// MCP tools in this sample return these to the caller so the adversarial
/// client (and this file's own documentation) can show exactly why a claim
/// was not honored.
/// </summary>
public sealed record RejectedClaim(string Key, string? Value, string Reason);

/// <summary>
/// The result of validating an untrusted, client-supplied claim set. Only
/// <see cref="Accepted"/> is ever handed to <see cref="StoreChainAuthorizationPolicy"/>.
/// Nothing in <see cref="Rejected"/> is used for any authorization decision.
/// </summary>
public sealed record ClaimsValidationResult(
    IReadOnlyDictionary<string, string> Accepted,
    IReadOnlyList<RejectedClaim> Rejected,
    bool IsSpoofingAttempt)
{
    public static ClaimsValidationResult Empty { get; } =
        new(new Dictionary<string, string>(), [], IsSpoofingAttempt: false);
}

/// <summary>
/// Validates claims sent by the MCP caller itself, as distinct from the
/// server-derived identity produced by <c>Authenticate(actor, token)</c>.
///
/// The distinction matters: <b>identity</b> (tenant, role) is never taken
/// from the caller — it is resolved server-side from the actor/token pair,
/// exactly as before this feature was added. <b>Claims</b> are additional,
/// caller-asserted context (scope narrowing, resource scoping, operational
/// evidence) that the caller volunteers on top of that identity.
///
/// Because claims arrive over an untrusted transport (any MCP client can
/// send any JSON it likes), this validator treats every claim as hostile
/// until proven otherwise:
///
///  1. Reserved identity keys (role, tenant, actor, admin flags, ...) are
///     never accepted under any circumstances, even if their value happens
///     to match the caller's real identity. Their mere presence is treated
///     as a spoofing attempt and fails the entire request closed.
///  2. Recognized non-identity keys are validated against a strict format
///     for that key. A malformed value is rejected individually; it does
///     not by itself fail the rest of the request, but any privilege that
///     depended on it is then evaluated as if the claim were absent.
///  3. Unrecognized keys are dropped individually (fail-closed on trust,
///     fail-open on noise): the request proceeds without them, and the
///     rejection is reported back so the caller can see what was ignored.
///  4. Every accepted claim can only ever narrow what the policy already
///     allows for the authenticated role. Claims are never additive to
///     privilege — see <see cref="StoreChainAuthorizationPolicy"/> for how
///     each accepted claim is consumed.
/// </summary>
public static class ClientClaimsValidator
{
    /// <summary>
    /// Claim keys that describe identity or privilege directly. These can
    /// only be established through authentication, never through a claim.
    /// Their presence in a client claim set is itself the attack: it means
    /// the caller is trying to assert something the server should be
    /// deciding on its behalf.
    /// </summary>
    private static readonly HashSet<string> ReservedIdentityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "role", "tenant", "tenantid", "actor", "isadmin", "admin", "permissions", "capabilities", "scopes"
    };

    private static readonly HashSet<string> RecognizedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "scope", "warehouse", "max_rows", "reason", "change_ticket", "not_after"
    };

    private static readonly Regex ChangeTicketPattern = new(@"^CHG-\d{4,}$", RegexOptions.Compiled);

    public static ClaimsValidationResult Validate(
        IReadOnlyDictionary<string, string>? rawClaims,
        DateTimeOffset now)
    {
        if (rawClaims is null || rawClaims.Count == 0)
            return ClaimsValidationResult.Empty;

        // Step 1: any reserved identity key present at all is a hard, whole-request
        // failure. We do not selectively drop it and continue, because a caller
        // that tries this once is demonstrating intent to spoof identity, and
        // partially processing the rest of the call would still leak information
        // about which other claims *would* have been honored.
        var spoofedKeys = rawClaims.Keys.Where(k => ReservedIdentityKeys.Contains(k)).ToArray();
        if (spoofedKeys.Length > 0)
        {
            var rejections = spoofedKeys
                .Select(k => new RejectedClaim(k, rawClaims[k],
                    "Identity/privilege must come from authentication, not from a client-supplied claim."))
                .ToArray();
            return new ClaimsValidationResult(new Dictionary<string, string>(), rejections, IsSpoofingAttempt: true);
        }

        var accepted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rejected = new List<RejectedClaim>();

        foreach (var (key, value) in rawClaims)
        {
            if (!RecognizedKeys.Contains(key))
            {
                rejected.Add(new RejectedClaim(key, value, "Unrecognized claim key; ignored."));
                continue;
            }

            var (ok, reason) = ValidateFormat(key, value);
            if (!ok)
            {
                rejected.Add(new RejectedClaim(key, value, reason!));
                continue;
            }

            accepted[key] = value;
        }

        // Step 2: cross-field validation. "not_after" only makes sense paired
        // with the evidence it is meant to expire; on its own it is accepted
        // as a format-valid timestamp but has no effect.
        if (accepted.TryGetValue("not_after", out var notAfterRaw) &&
            DateTimeOffset.TryParse(notAfterRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var notAfter) &&
            notAfter < now)
        {
            accepted.Remove("not_after");
            // Any evidence this staleness was meant to bound must fail closed too.
            if (accepted.Remove("reason"))
                rejected.Add(new RejectedClaim("reason", rawClaims.GetValueOrDefault("reason"),
                    $"Associated 'not_after' claim ({notAfterRaw}) has expired; evidence is stale."));
            if (accepted.Remove("change_ticket"))
                rejected.Add(new RejectedClaim("change_ticket", rawClaims.GetValueOrDefault("change_ticket"),
                    $"Associated 'not_after' claim ({notAfterRaw}) has expired; evidence is stale."));
            rejected.Add(new RejectedClaim("not_after", notAfterRaw, "Timestamp is in the past."));
        }

        return new ClaimsValidationResult(accepted, rejected, IsSpoofingAttempt: false);
    }

    private static (bool Ok, string? Reason) ValidateFormat(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (false, "Value is empty.");

        switch (key.ToLowerInvariant())
        {
            case "scope":
                return value is "read-only" or "full"
                    ? (true, null)
                    : (false, "Must be 'read-only' or 'full'.");

            case "warehouse":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var warehouseId) && warehouseId > 0
                    ? (true, null)
                    : (false, "Must be a positive integer warehouse id.");

            case "max_rows":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var maxRows) && maxRows is > 0 and <= 10_000
                    ? (true, null)
                    : (false, "Must be a positive integer no greater than 10,000.");

            case "reason":
                return value.Trim().Length is >= 8 and <= 240
                    ? (true, null)
                    : (false, "Must be between 8 and 240 characters.");

            case "change_ticket":
                return ChangeTicketPattern.IsMatch(value)
                    ? (true, null)
                    : (false, "Must match 'CHG-####' (four or more digits).");

            case "not_after":
                return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)
                    ? (true, null)
                    : (false, "Must be a valid ISO-8601 timestamp.");

            default:
                // Unreachable for recognized keys, kept for exhaustiveness.
                return (false, "No format rule defined for this key.");
        }
    }
}
