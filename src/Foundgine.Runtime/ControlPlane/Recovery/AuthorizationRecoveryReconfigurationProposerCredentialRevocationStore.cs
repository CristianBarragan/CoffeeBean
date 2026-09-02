namespace Foundgine.Runtime.ControlPlane;

/// <summary>Durable, cross-instance state for proposer credential generations.</summary>
public sealed record AuthorizationRecoveryProposerCredentialDurableState(
    string ProposerId,
    string CredentialFingerprint,
    long CredentialSequence,
    AuthorizationRecoveryReconfigurationProposerCredentialState State);

/// <summary>
/// Authoritative persistence boundary for proposer credential lifecycle state. Implementations must
/// reject sequence rollback and must make writes visible atomically to all instances sharing the store.
/// </summary>
public interface IAuthorizationRecoveryProposerCredentialRevocationStore
{
    ValueTask<AuthorizationRecoveryProposerCredentialDurableState?> ReadAsync(
        string proposerId,
        CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        AuthorizationRecoveryProposerCredentialDurableState state,
        long expectedPreviousSequence,
        CancellationToken cancellationToken = default);
}

/// <summary>Reference/test implementation of the authoritative cross-instance store.</summary>
public sealed class InMemoryAuthorizationRecoveryProposerCredentialRevocationStore
    : IAuthorizationRecoveryProposerCredentialRevocationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AuthorizationRecoveryProposerCredentialDurableState> _states = new(StringComparer.Ordinal);

    public ValueTask<AuthorizationRecoveryProposerCredentialDurableState?> ReadAsync(
        string proposerId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return ValueTask.FromResult(
                _states.TryGetValue(proposerId, out var state) ? state : null);
    }

    public ValueTask WriteAsync(
        AuthorizationRecoveryProposerCredentialDurableState state,
        long expectedPreviousSequence,
        CancellationToken cancellationToken = default)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        lock (_gate)
        {
            var actual = _states.TryGetValue(state.ProposerId, out var existing)
                ? existing.CredentialSequence
                : 0;
            if (actual != expectedPreviousSequence)
                throw new AuthorizationRecoveryProposerCredentialRevocationConflictException(
                    state.ProposerId, expectedPreviousSequence, actual);
            if (state.CredentialSequence <= actual)
                throw new AuthorizationRecoveryProposerCredentialRevocationConflictException(
                    state.ProposerId, actual + 1, state.CredentialSequence);
            _states[state.ProposerId] = state;
        }
        return ValueTask.CompletedTask;
    }
}

public sealed class AuthorizationRecoveryProposerCredentialRevocationConflictException : InvalidOperationException
{
    public AuthorizationRecoveryProposerCredentialRevocationConflictException(
        string proposerId,
        long expectedSequence,
        long actualSequence)
        : base($"Proposer '{proposerId}' credential state changed concurrently or attempted rollback: expected sequence {expectedSequence}, actual sequence {actualSequence}.")
    {
        ProposerId = proposerId;
        ExpectedSequence = expectedSequence;
        ActualSequence = actualSequence;
    }

    public string ProposerId { get; }
    public long ExpectedSequence { get; }
    public long ActualSequence { get; }
}
