namespace Foundgine.Security.Authority;

/// <summary>
/// Explicit authority term for the recovery anchor. The term fences stale leaders after
/// partition/recovery: only the currently elected authority may perform an advance.
/// </summary>
public sealed record AuthorizationRecoveryAuthorityState
{
    public long Term { get; }
    public string AuthorityId { get; }

    public AuthorizationRecoveryAuthorityState(long term, string authorityId)
    {
        Term = term;
        AuthorityId = ValidateAuthority(authorityId);
    }

    private static string ValidateAuthority(string authorityId) =>
        !string.IsNullOrWhiteSpace(authorityId)
            ? authorityId
            : throw new ArgumentException("Authority identity is required.", nameof(authorityId));
}

/// <summary>
/// Authoritative recovery anchor boundary. A quorum can establish that a candidate is safe to
/// evaluate, but only the currently fenced authority may mutate the single authoritative anchor.
/// The term is monotonic and must be presented on every write, preventing a stale partition from
/// regaining write authority after failover.
/// </summary>
public interface IAuthorizationRecoveryAuthorityAnchor
{
    ValueTask<AuthorizationRecoveryAuthorityState> ReadAuthorityAsync(CancellationToken cancellationToken = default);
    ValueTask<string> ReadAuthorityCertificateDigestAsync(CancellationToken cancellationToken = default);

    /// <summary>Installs a strictly newer authority term. Reconfiguration is itself fenced.</summary>
    ValueTask<bool> TryInstallAuthorityAsync(
        long expectedTerm,
        string expectedAuthorityId,
        long newTerm,
        string newAuthorityId,
        CancellationToken cancellationToken = default);

    /// <summary>Advances recovery state only when both recovery state and authority term match.</summary>
    ValueTask<bool> TryInstallAuthorityCertificateAsync(
        AuthorizationRecoveryAuthorityTermCertificate certificate,
        ReadOnlyMemory<byte> previousAuthoritySigningKey,
        CancellationToken cancellationToken = default);

    ValueTask<bool> TryAdvanceAsync(
        long authorityTerm,
        string authorityId,
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reference implementation used by adversarial tests. Production implementations must persist
/// authority term and recovery state in the same strongly consistent control plane, or otherwise
/// provide equivalent linearizable fencing semantics.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryAuthorityAnchor : IAuthorizationRecoveryAuthorityAnchor
{
    private readonly object _gate = new();
    private AuthorizationRecoveryAuthorityState _authority;
    private string _authorityCertificateDigest;
    private AuthorizationRecoveryAnchorState _state = AuthorizationRecoveryAnchorState.Empty;

    public InMemoryAuthorizationRecoveryAuthorityAnchor(string initialAuthorityId = "authority-0")
    {
        if (string.IsNullOrWhiteSpace(initialAuthorityId))
            throw new ArgumentException("Authority identity is required.", nameof(initialAuthorityId));
        _authority = new AuthorizationRecoveryAuthorityState(1, initialAuthorityId);
        _authorityCertificateDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("foundgine-authority-genesis/v1|1|" + initialAuthorityId))).ToLowerInvariant();
    }

    public ValueTask<AuthorizationRecoveryAuthorityState> ReadAuthorityAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_authority);
    }

    public ValueTask<string> ReadAuthorityCertificateDigestAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_authorityCertificateDigest);
    }

    public ValueTask<bool> TryInstallAuthorityCertificateAsync(
        AuthorizationRecoveryAuthorityTermCertificate certificate,
        ReadOnlyMemory<byte> previousAuthoritySigningKey,
        CancellationToken cancellationToken = default)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));
        lock (_gate)
        {
            if (!certificate.Verify(previousAuthoritySigningKey.Span, _authority))
                return ValueTask.FromResult(false);

            if (!string.Equals(certificate.PreviousCertificateDigest, _authorityCertificateDigest, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(false);

            _authority = new AuthorizationRecoveryAuthorityState(certificate.NewTerm, certificate.NewAuthorityId);
            _authorityCertificateDigest = certificate.Digest();
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> TryInstallAuthorityAsync(
        long expectedTerm,
        string expectedAuthorityId,
        long newTerm,
        string newAuthorityId,
        CancellationToken cancellationToken = default)
    {
        if (expectedTerm < 1) throw new ArgumentOutOfRangeException(nameof(expectedTerm));
        if (string.IsNullOrWhiteSpace(expectedAuthorityId)) throw new ArgumentException("Expected authority identity is required.", nameof(expectedAuthorityId));
        if (string.IsNullOrWhiteSpace(newAuthorityId)) throw new ArgumentException("New authority identity is required.", nameof(newAuthorityId));

        lock (_gate)
        {
            // A caller proposing a non-adjacent term (skipping ahead, or
            // reinstalling a term that's already gone) is exactly as stale
            // as one proposing the wrong current term/authority -- fence it
            // the same way (return false) rather than throwing, since a
            // lagging caller computing newTerm from its own stale view is
            // an expected fencing scenario, not a programming error.
            if (_authority.Term != expectedTerm ||
                !string.Equals(_authority.AuthorityId, expectedAuthorityId, StringComparison.Ordinal) ||
                newTerm != expectedTerm + 1)
                return ValueTask.FromResult(false);

            _authority = new AuthorizationRecoveryAuthorityState(newTerm, newAuthorityId);
            _authorityCertificateDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"foundgine-authority-legacy/v1|{expectedTerm}|{expectedAuthorityId}|{newTerm}|{newAuthorityId}|{_authorityCertificateDigest}"))).ToLowerInvariant();
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> TryAdvanceAsync(
        long authorityTerm,
        string authorityId,
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        CancellationToken cancellationToken = default)
    {
        if (authorityTerm < 1) throw new ArgumentOutOfRangeException(nameof(authorityTerm));
        if (string.IsNullOrWhiteSpace(authorityId)) throw new ArgumentException("Authority identity is required.", nameof(authorityId));

        lock (_gate)
        {
            if (_authority.Term != authorityTerm || !string.Equals(_authority.AuthorityId, authorityId, StringComparison.Ordinal))
                return ValueTask.FromResult(false);

            if (_state.Sequence != expectedSequence || !string.Equals(_state.Digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(false);

            if (nextSequence != expectedSequence + 1)
                throw new ArgumentException("Recovery anchor transitions must advance exactly one sequence.", nameof(nextSequence));

            _state = new AuthorizationRecoveryAnchorState(nextSequence, nextDigest.ToLowerInvariant(), authorityId);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<AuthorizationRecoveryAnchorState> ReadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_state);
    }
}