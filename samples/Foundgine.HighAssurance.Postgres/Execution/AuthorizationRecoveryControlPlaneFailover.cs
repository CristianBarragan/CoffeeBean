namespace Foundgine.HighAssurance.Postgres.Execution;

public enum AuthorizationRecoveryControlPlaneRole
{
    Standby,
    Active,
    Retired
}

public sealed record AuthorizationRecoveryControlPlaneEpoch(
    long Epoch,
    long Sequence,
    string Digest,
    string ControlPlaneId,
    AuthorizationRecoveryControlPlaneRole Role)
{
    public static AuthorizationRecoveryControlPlaneEpoch Genesis(string controlPlaneId) =>
        new(1, 0, AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest,
            controlPlaneId, AuthorizationRecoveryControlPlaneRole.Standby);
}

public sealed class AuthorizationRecoveryControlPlaneFailoverException : Exception
{
    public AuthorizationRecoveryControlPlaneFailoverException(string message) : base(message) { }
}

/// <summary>
/// Represents the independently authoritative control-plane failover state. A successor may
/// activate only by proving the same anchored history and advancing the authority epoch. The
/// successor never creates a new trust root merely because the primary is unreachable.
/// </summary>
public interface IAuthorizationRecoveryControlPlaneFailoverAuthority
{
    ValueTask<AuthorizationRecoveryControlPlaneEpoch> ReadAsync(CancellationToken cancellationToken = default);

    ValueTask<bool> TryActivateSuccessorAsync(
        long expectedEpoch,
        long expectedSequence,
        string expectedDigest,
        string successorId,
        CancellationToken cancellationToken = default);
}

/// <summary>Reference/test implementation of a linearizable failover authority.</summary>
public sealed class InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority
    : IAuthorizationRecoveryControlPlaneFailoverAuthority
{
    private readonly object _gate = new();
    private AuthorizationRecoveryControlPlaneEpoch _state;

    public InMemoryAuthorizationRecoveryControlPlaneFailoverAuthority(
        string primaryId,
        long sequence = 0,
        string? digest = null)
    {
        if (string.IsNullOrWhiteSpace(primaryId)) throw new ArgumentException("Primary control-plane ID is required.", nameof(primaryId));
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        _state = new(1, sequence, digest ?? AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.GenesisDigest,
            primaryId, AuthorizationRecoveryControlPlaneRole.Active);
    }

    public ValueTask<AuthorizationRecoveryControlPlaneEpoch> ReadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_state);
    }

    public ValueTask<bool> TryActivateSuccessorAsync(
        long expectedEpoch,
        long expectedSequence,
        string expectedDigest,
        string successorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(successorId)) throw new ArgumentException("Successor ID is required.", nameof(successorId));
        if (expectedEpoch <= 0) throw new ArgumentOutOfRangeException(nameof(expectedEpoch));
        if (expectedSequence < 0) throw new ArgumentOutOfRangeException(nameof(expectedSequence));
        if (string.IsNullOrWhiteSpace(expectedDigest)) throw new ArgumentException("Expected digest is required.", nameof(expectedDigest));

        lock (_gate)
        {
            if (_state.Epoch != expectedEpoch || _state.Sequence != expectedSequence ||
                !string.Equals(_state.Digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(false);

            if (_state.Role != AuthorizationRecoveryControlPlaneRole.Active)
                return ValueTask.FromResult(false);

            _state = new(_state.Epoch + 1, _state.Sequence, _state.Digest,
                successorId, AuthorizationRecoveryControlPlaneRole.Active);
            return ValueTask.FromResult(true);
        }
    }
}

/// <summary>
/// Failover coordinator. It first proves that the successor's recovered ledger exactly matches
/// the last anchored head, then performs an epoch CAS. No new audit authority can be created
/// until the successor owns the next epoch.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneFailoverCoordinator
{
    private readonly IAuthorizationRecoveryControlPlaneFailoverAuthority _authority;
    private readonly IAuthorizationRecoveryProposerCredentialAuditHeadAnchor _anchor;

    public AuthorizationRecoveryControlPlaneFailoverCoordinator(
        IAuthorizationRecoveryControlPlaneFailoverAuthority authority,
        IAuthorizationRecoveryProposerCredentialAuditHeadAnchor anchor)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
    }

    public async ValueTask<AuthorizationRecoveryControlPlaneEpoch> FailoverAsync(
        AuthorizationRecoveryProposerCredentialAuditLedger recoveredLedger,
        string successorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveredLedger);
        if (string.IsNullOrWhiteSpace(successorId)) throw new ArgumentException("Successor ID is required.", nameof(successorId));

        var current = await _authority.ReadAsync(cancellationToken);
        if (current.Role != AuthorizationRecoveryControlPlaneRole.Active)
            throw new AuthorizationRecoveryControlPlaneFailoverException("The current control plane is not active.");

        await recoveredLedger.VerifyAgainstAnchorAsync(_anchor, cancellationToken);
        var head = recoveredLedger.HeadState;
        if (head.Sequence != current.Sequence || !string.Equals(head.Digest, current.Digest, StringComparison.OrdinalIgnoreCase))
            throw new AuthorizationRecoveryControlPlaneFailoverException("Recovered history does not match the active control-plane epoch head.");

        var activated = await _authority.TryActivateSuccessorAsync(
            current.Epoch, current.Sequence, current.Digest, successorId, cancellationToken);
        if (!activated)
            throw new AuthorizationRecoveryControlPlaneFailoverException("Control-plane failover lost the epoch race or the authoritative state changed.");

        return await _authority.ReadAsync(cancellationToken);
    }
}


/// <summary>
/// Represents a recovered control plane that has reconciled to the currently authoritative
/// epoch and therefore may rejoin only as a non-authoritative standby.
/// </summary>
public sealed record AuthorizationRecoveryControlPlaneRejoinState(
    long Epoch,
    long Sequence,
    string Digest,
    string ControlPlaneId,
    AuthorizationRecoveryControlPlaneRole Role)
{
    public static AuthorizationRecoveryControlPlaneRejoinState Standby(
        AuthorizationRecoveryControlPlaneEpoch authoritative,
        string controlPlaneId) =>
        new(authoritative.Epoch, authoritative.Sequence, authoritative.Digest,
            controlPlaneId, AuthorizationRecoveryControlPlaneRole.Standby);
}

/// <summary>
/// Reconciles a returning control plane. A node that was previously primary is never allowed
/// to resume its old authority merely because its local state still says it was active.
/// It must first prove the current externally anchored history and observe the current epoch;
/// the result is always non-authoritative standby state.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRejoinCoordinator
{
    private readonly IAuthorizationRecoveryControlPlaneFailoverAuthority _authority;
    private readonly IAuthorizationRecoveryProposerCredentialAuditHeadAnchor _anchor;

    public AuthorizationRecoveryControlPlaneRejoinCoordinator(
        IAuthorizationRecoveryControlPlaneFailoverAuthority authority,
        IAuthorizationRecoveryProposerCredentialAuditHeadAnchor anchor)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
    }

    public async ValueTask<AuthorizationRecoveryControlPlaneRejoinState> RejoinAsStandbyAsync(
        AuthorizationRecoveryProposerCredentialAuditLedger recoveredLedger,
        string returningControlPlaneId,
        long locallyObservedEpoch,
        long locallyObservedSequence,
        string locallyObservedDigest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveredLedger);
        if (string.IsNullOrWhiteSpace(returningControlPlaneId))
            throw new ArgumentException("Returning control-plane ID is required.", nameof(returningControlPlaneId));
        if (locallyObservedEpoch <= 0) throw new ArgumentOutOfRangeException(nameof(locallyObservedEpoch));
        if (locallyObservedSequence < 0) throw new ArgumentOutOfRangeException(nameof(locallyObservedSequence));
        if (string.IsNullOrWhiteSpace(locallyObservedDigest))
            throw new ArgumentException("Local digest is required.", nameof(locallyObservedDigest));

        var current = await _authority.ReadAsync(cancellationToken);
        if (current.Role != AuthorizationRecoveryControlPlaneRole.Active)
            throw new AuthorizationRecoveryControlPlaneFailoverException("No active control plane is available for rejoin reconciliation.");

        await recoveredLedger.VerifyAgainstAnchorAsync(_anchor, cancellationToken);
        var head = recoveredLedger.HeadState;
        if (head.Sequence != current.Sequence ||
            !string.Equals(head.Digest, current.Digest, StringComparison.OrdinalIgnoreCase))
            throw new AuthorizationRecoveryControlPlaneFailoverException(
                "Returning history does not match the authoritative anchored head.");

        // A returning node may have stale local state. It is reconciled, never promoted.
        if (locallyObservedEpoch != current.Epoch ||
            locallyObservedSequence != current.Sequence ||
            !string.Equals(locallyObservedDigest, current.Digest, StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationRecoveryControlPlaneRejoinState.Standby(current, returningControlPlaneId);
        }

        return AuthorizationRecoveryControlPlaneRejoinState.Standby(current, returningControlPlaneId);
    }
}
