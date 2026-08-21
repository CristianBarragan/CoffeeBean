using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>Rollback-resistant external anchor for proposer-credential audit history.</summary>
public interface IAuthorizationRecoveryProposerCredentialAuditHeadAnchor
{
    ValueTask<AuthorizationRecoveryProposerCredentialAuditHeadAnchorState> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Advances the anchored audit head only when sequence and digest still match.</summary>
    ValueTask<bool> TryAdvanceAsync(
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        string writerId,
        CancellationToken cancellationToken = default);
}

public sealed record AuthorizationRecoveryProposerCredentialAuditHeadAnchorState(
    long Sequence,
    string Digest,
    string? WriterId)
{
    public const string GenesisDigest = AuthorizationRecoveryAnchorState.GenesisDigest;

    public static AuthorizationRecoveryProposerCredentialAuditHeadAnchorState Empty { get; } =
        new(0, GenesisDigest, null);
}

/// <summary>
/// Reference implementation for adversarial tests. Production must use an independent,
/// durable, linearizable control-plane/KMS/HSM/transparency anchor. The audit ledger itself
/// must never be treated as the sole rollback trust root.
/// </summary>
public sealed class InMemoryAuthorizationRecoveryProposerCredentialAuditHeadAnchor
    : IAuthorizationRecoveryProposerCredentialAuditHeadAnchor
{
    private readonly object _gate = new();
    private AuthorizationRecoveryProposerCredentialAuditHeadAnchorState _state =
        AuthorizationRecoveryProposerCredentialAuditHeadAnchorState.Empty;

    public ValueTask<AuthorizationRecoveryProposerCredentialAuditHeadAnchorState> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_gate) return ValueTask.FromResult(_state);
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
            throw new ArgumentException("Audit head transitions must advance exactly one sequence.", nameof(nextSequence));
        if (string.IsNullOrWhiteSpace(writerId)) throw new ArgumentException("Writer identity is required.", nameof(writerId));
        ValidateDigest(expectedDigest, nameof(expectedDigest));
        ValidateDigest(nextDigest, nameof(nextDigest));

        lock (_gate)
        {
            if (_state.Sequence != expectedSequence || !FixedEquals(_state.Digest, expectedDigest))
                return ValueTask.FromResult(false);

            _state = new AuthorizationRecoveryProposerCredentialAuditHeadAnchorState(
                nextSequence, nextDigest.ToLowerInvariant(), writerId);
            return ValueTask.FromResult(true);
        }
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(right.ToLowerInvariant()));

    private static void ValidateDigest(string digest, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(digest) || digest.Length != 64)
            throw new ArgumentException("Audit head digests must be 64 hexadecimal characters.", parameterName);
        _ = Convert.FromHexString(digest);
    }
}

public sealed class AuthorizationRecoveryProposerCredentialAuditHeadRollbackException : Exception
{
    public AuthorizationRecoveryProposerCredentialAuditHeadRollbackException(
        long ledgerSequence,
        string ledgerDigest,
        long anchoredSequence,
        string anchoredDigest)
        : base($"Proposer credential audit history is behind or diverges from the external anchor. Ledger={ledgerSequence}/{ledgerDigest}; Anchor={anchoredSequence}/{anchoredDigest}.")
    {
        LedgerSequence = ledgerSequence;
        LedgerDigest = ledgerDigest;
        AnchoredSequence = anchoredSequence;
        AnchoredDigest = anchoredDigest;
    }

    public long LedgerSequence { get; }
    public string LedgerDigest { get; }
    public long AnchoredSequence { get; }
    public string AnchoredDigest { get; }
}

public sealed class AuthorizationRecoveryProposerCredentialAuditHeadForkException : Exception
{
    public AuthorizationRecoveryProposerCredentialAuditHeadForkException(
        long sequence,
        string ledgerDigest,
        string anchoredDigest)
        : base($"Proposer credential audit history fork detected at sequence {sequence}. Ledger digest {ledgerDigest} differs from anchored digest {anchoredDigest}.")
    {
        Sequence = sequence;
        LedgerDigest = ledgerDigest;
        AnchoredDigest = anchoredDigest;
    }

    public long Sequence { get; }
    public string LedgerDigest { get; }
    public string AnchoredDigest { get; }
}
