namespace Foundgine.Authorization;

public enum AuthorizationRecoveryAuditAnchorAvailability
{
    Available,
    Unavailable,
    Degraded
}

public sealed record AuthorizationRecoveryProposerCredentialAuditAnchorAvailabilityState(
    AuthorizationRecoveryAuditAnchorAvailability Status,
    DateTimeOffset ObservedAtUtc,
    string? Reason)
{
    public bool CanCreateNewAuthority => Status == AuthorizationRecoveryAuditAnchorAvailability.Available;
}

/// <summary>Separates read-only verification of an already anchored head from authority-creating advancement.</summary>
public interface IAuthorizationRecoveryProposerCredentialAuditHeadAvailability
{
    ValueTask<AuthorizationRecoveryProposerCredentialAuditAnchorAvailabilityState> GetAvailabilityAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException : Exception
{
    public AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException(string operation, string? reason)
        : base($"The proposer-credential audit anchor is not available for {operation}; no new authority may be created. {reason}")
    {
        Operation = operation;
        Reason = reason;
    }

    public string Operation { get; }
    public string? Reason { get; }
}

/// <summary>
/// Reference availability gate. Existing anchored history may still be verified from a trusted
/// cached snapshot, but creating a new audit head requires the anchor to be reachable and available.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAvailability
    : IAuthorizationRecoveryProposerCredentialAuditHeadAvailability
{
    private readonly object _gate = new();
    private AuthorizationRecoveryProposerCredentialAuditAnchorAvailabilityState _state =
        new(AuthorizationRecoveryAuditAnchorAvailability.Available, DateTimeOffset.UtcNow, null);

    public ValueTask<AuthorizationRecoveryProposerCredentialAuditAnchorAvailabilityState> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_state);
    }

    public void SetAvailable() => Set(AuthorizationRecoveryAuditAnchorAvailability.Available, null);
    public void SetUnavailable(string reason) => Set(AuthorizationRecoveryAuditAnchorAvailability.Unavailable, reason);
    public void SetDegraded(string reason) => Set(AuthorizationRecoveryAuditAnchorAvailability.Degraded, reason);

    private void Set(AuthorizationRecoveryAuditAnchorAvailability status, string? reason)
    {
        lock (_gate) _state = new(status, DateTimeOffset.UtcNow, reason);
    }
}

/// <summary>Composition of an audit head anchor with an explicit availability gate.</summary>
public sealed class AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor
    : IAuthorizationRecoveryProposerCredentialAuditHeadAnchor
{
    private readonly IAuthorizationRecoveryProposerCredentialAuditHeadAnchor _inner;
    private readonly IAuthorizationRecoveryProposerCredentialAuditHeadAvailability _availability;

    public AvailableOnlyAuthorizationRecoveryProposerCredentialAuditHeadAnchor(
        IAuthorizationRecoveryProposerCredentialAuditHeadAnchor inner,
        IAuthorizationRecoveryProposerCredentialAuditHeadAvailability availability)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
    }

    public ValueTask<AuthorizationRecoveryProposerCredentialAuditHeadAnchorState> ReadAsync(CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(cancellationToken);

    public async ValueTask<bool> TryAdvanceAsync(long expectedSequence, string expectedDigest, long nextSequence, string nextDigest, string writerId, CancellationToken cancellationToken = default)
    {
        var state = await _availability.GetAvailabilityAsync(cancellationToken);
        if (!state.CanCreateNewAuthority)
            throw new AuthorizationRecoveryProposerCredentialAuditAnchorUnavailableException("audit-head advancement", state.Reason);
        return await _inner.TryAdvanceAsync(expectedSequence, expectedDigest, nextSequence, nextDigest, writerId, cancellationToken);
    }
}
