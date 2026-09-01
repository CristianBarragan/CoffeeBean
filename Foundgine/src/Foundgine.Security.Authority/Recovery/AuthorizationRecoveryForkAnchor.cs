using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Security.Authority;

/// <summary>
/// Shared rollback-resistant authority anchor. A recovery history is identified by both
/// its monotonic sequence and the digest of the committed state at that sequence.
/// Implementations must provide a linearizable compare-and-advance operation.
/// </summary>
public interface IAuthorizationRecoveryForkAnchor
{
    ValueTask<AuthorizationRecoveryAnchorState> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the single authoritative history only when the supplied expected state is
    /// still current. A competing writer therefore loses its compare-and-swap instead of
    /// creating a second authoritative branch.
    /// </summary>
    ValueTask<bool> TryAdvanceAsync(
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        string writerId,
        CancellationToken cancellationToken = default);
}

public sealed record AuthorizationRecoveryAnchorState(
    long Sequence,
    string Digest,
    string? WriterId)
{
    /// <summary>
    /// Canonical genesis digest: 64 hex zero characters. <see cref="InMemoryAuthorizationRecoveryForkAnchor"/>
    /// validates every digest it is given — including the expected digest on the very first
    /// transition — as exactly 64 hex characters; there is no separate "no history yet" sentinel
    /// shape. <see cref="Empty"/> must therefore use this value, not <see cref="string.Empty"/>, or
    /// genesis could never validly be advanced by any caller.
    /// </summary>
    public const string GenesisDigest = "0000000000000000000000000000000000000000000000000000000000000000";

    public static AuthorizationRecoveryAnchorState Empty { get; } =
        new(0, GenesisDigest, null);
}

/// <summary>
/// Reference implementation for adversarial/unit tests. Production deployments must use
/// a shared durable control-plane/KMS/HSM/ledger implementation whose compare-and-advance
/// operation is linearizable across all application instances.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryForkAnchor : IAuthorizationRecoveryForkAnchor
{
    private readonly object _gate = new();
    private AuthorizationRecoveryAnchorState _state = AuthorizationRecoveryAnchorState.Empty;

    public ValueTask<AuthorizationRecoveryAnchorState> ReadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return ValueTask.FromResult(_state);
    }

    public ValueTask<bool> TryAdvanceAsync(
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        string writerId,
        CancellationToken cancellationToken = default)
    {
        if (expectedSequence < 0) throw new ArgumentOutOfRangeException(nameof(expectedSequence));
        if (nextSequence != expectedSequence + 1)
            throw new ArgumentException("Recovery anchor transitions must advance exactly one sequence.", nameof(nextSequence));
        if (string.IsNullOrWhiteSpace(writerId)) throw new ArgumentException("Writer identity is required.", nameof(writerId));
        ValidateDigest(expectedDigest, nameof(expectedDigest));
        ValidateDigest(nextDigest, nameof(nextDigest));

        lock (_gate)
        {
            if (_state.Sequence != expectedSequence ||
                !FixedEquals(_state.Digest, expectedDigest))
                return ValueTask.FromResult(false);

            _state = new AuthorizationRecoveryAnchorState(nextSequence, nextDigest.ToLowerInvariant(), writerId);
            return ValueTask.FromResult(true);
        }
    }

    private static bool FixedEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(right.ToLowerInvariant()));

    private static void ValidateDigest(string digest, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(digest) || digest.Length != 64)
            throw new ArgumentException("Recovery digests must be 64 hexadecimal characters.", parameterName);
        _ = Convert.FromHexString(digest);
    }
}
