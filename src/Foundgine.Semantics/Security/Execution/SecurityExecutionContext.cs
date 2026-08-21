using Foundgine.Semantics.Security.Warrants;

namespace Foundgine.Semantics.Security.Execution;

/// <summary>
/// Untrusted-caller security context carried with a semantic request. The
/// warrant is evidence of authority; the engine still verifies it and checks
/// it against the resolved capability at execution time.
/// </summary>
public sealed record SecurityExecutionContext(
    SecurityWarrant Warrant,
    string Subject,
    string Audience,
    string? Tenant = null,
    string? ResourceScope = null)
{
    /// <summary>
    /// Stable authority partition used to prevent cross-warrant provider-plan cache reuse.
    /// The full warrant digest is intentional and includes nonce/signature, making the
    /// cache boundary conservative rather than attempting to infer authority equivalence.
    /// </summary>
    public string AuthorityCachePartition =>
        $"{Subject}|{Audience}|{Tenant ?? "-"}|{ResourceScope ?? "-"}|{Warrant.Digest}";
}
