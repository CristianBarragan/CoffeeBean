using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>Outcome of a proposer credential lifecycle transition.</summary>
public enum AuthorizationRecoveryRepairProposerCredentialLifecycleResult
{
    Rotated,
    AlreadyCurrent,
    UnknownProposer,
    InvalidTransition,
    Revoked,
    Retired
}

/// <summary>Atomic authorization result for an in-flight repair attempt.</summary>
public enum AuthorizationRecoveryRepairProposerCredentialAttemptResult
{
    Authorized,
    UnknownProposer,
    InvalidCredential,
    CredentialNotActive,
    CredentialSequenceMismatch,
    CredentialFingerprintMismatch,
    TransactionBindingMismatch,
    StateBindingMismatch,
    ProofMismatch
}

/// <summary>
/// M5.70 lifecycle fence for repair proposer credentials. Rotation and
/// revocation linearize against authorization under one control-plane lock.
/// An attempt that acquires the lock before a lifecycle transition may finish
/// under the old credential; an attempt that acquires it after the transition
/// is rejected. There is no check-then-use gap between lifecycle validation
/// and authorization acceptance.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle
{
    private sealed record Registration(
        string CredentialId,
        string Fingerprint,
        long Sequence,
        AuthorizationRecoveryRepairProposerCredentialState State,
        byte[] ProofKey);

    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _proposers = new(StringComparer.Ordinal);

    public void Register(string proposerId, string credentialId, string fingerprint, ReadOnlySpan<byte> proofKey, long sequence = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));

        lock (_gate)
        {
            _proposers[proposerId] = new(credentialId, fingerprint, sequence,
                AuthorizationRecoveryRepairProposerCredentialState.Active, proofKey.ToArray());
        }
    }

    public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Rotate(
        string proposerId, string credentialId, string fingerprint, ReadOnlySpan<byte> proofKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));

        lock (_gate)
        {
            if (!_proposers.TryGetValue(proposerId, out var current))
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
            if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;
            if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Retired)
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Retired;
            if (string.Equals(current.CredentialId, credentialId, StringComparison.Ordinal) &&
                string.Equals(current.Fingerprint, fingerprint, StringComparison.Ordinal))
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.AlreadyCurrent;

            _proposers[proposerId] = new(credentialId, fingerprint, checked(current.Sequence + 1),
                AuthorizationRecoveryRepairProposerCredentialState.Active, proofKey.ToArray());
            return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
        }
    }

    public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Revoke(string proposerId)
    {
        lock (_gate)
        {
            if (!_proposers.TryGetValue(proposerId, out var current))
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
            if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;

            _proposers[proposerId] = current with { State = AuthorizationRecoveryRepairProposerCredentialState.Revoked };
            return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
        }
    }

    public AuthorizationRecoveryRepairProposerCredentialLifecycleResult Retire(string proposerId)
    {
        lock (_gate)
        {
            if (!_proposers.TryGetValue(proposerId, out var current))
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.UnknownProposer;
            if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Revoked)
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Revoked;
            if (current.State == AuthorizationRecoveryRepairProposerCredentialState.Retired)
                return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Retired;

            _proposers[proposerId] = current with { State = AuthorizationRecoveryRepairProposerCredentialState.Retired };
            return AuthorizationRecoveryRepairProposerCredentialLifecycleResult.Rotated;
        }
    }

    /// <summary>
    /// Performs credential lifecycle validation and proof verification while
    /// holding the same lock used by Rotate/Revoke/Retire. This is the M5.70
    /// linearization point for authorization.
    /// </summary>
    public AuthorizationRecoveryRepairProposerCredentialAttemptResult Authorize(
        AuthorizationRecoveryRepairProposerCredential credential)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.ProposerId) ||
            string.IsNullOrWhiteSpace(credential.CredentialId) || credential.CredentialSequence < 1 ||
            string.IsNullOrWhiteSpace(credential.TransactionId) || string.IsNullOrWhiteSpace(credential.ExpectedStateFingerprint) ||
            string.IsNullOrWhiteSpace(credential.TargetStateFingerprint))
            return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;

        lock (_gate)
        {
            if (!_proposers.TryGetValue(credential.ProposerId, out var current))
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.UnknownProposer;
            if (current.State != AuthorizationRecoveryRepairProposerCredentialState.Active)
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialNotActive;
            if (!string.Equals(credential.CredentialVersion, "v1", StringComparison.Ordinal))
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;
            if (credential.CredentialSequence != current.Sequence)
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialSequenceMismatch;
            if (!FixedEquals(credential.CredentialId, current.CredentialId))
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.InvalidCredential;
            if (!FixedEquals(credential.CredentialFingerprint, current.Fingerprint))
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.CredentialFingerprintMismatch;
            if (credential.TargetRevision != credential.ExpectedRevision + 1)
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.StateBindingMismatch;

            var supplied = TryDecode(credential.Proof);
            if (supplied is null)
                return AuthorizationRecoveryRepairProposerCredentialAttemptResult.ProofMismatch;

            using var hmac = new HMACSHA256(current.ProofKey);
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential)));
            return CryptographicOperations.FixedTimeEquals(supplied, expected)
                ? AuthorizationRecoveryRepairProposerCredentialAttemptResult.Authorized
                : AuthorizationRecoveryRepairProposerCredentialAttemptResult.ProofMismatch;
        }
    }

    public (string CredentialId, string Fingerprint, long Sequence, AuthorizationRecoveryRepairProposerCredentialState State) Snapshot(string proposerId)
    {
        lock (_gate)
        {
            if (!_proposers.TryGetValue(proposerId, out var current)) throw new KeyNotFoundException(proposerId);
            return (current.CredentialId, current.Fingerprint, current.Sequence, current.State);
        }
    }

    public static string CreateProof(AuthorizationRecoveryRepairProposerCredential credential, ReadOnlySpan<byte> proofKey)
    {
        using var hmac = new HMACSHA256(proofKey.ToArray());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential))));
    }

    private static byte[]? TryDecode(string proof)
    {
        try { return Convert.FromHexString(proof); }
        catch (FormatException) { return null; }
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static string Canonicalize(AuthorizationRecoveryRepairProposerCredential c)
    {
        static string F(string value) => $"{Encoding.UTF8.GetByteCount(value)}:{value}";
        return string.Concat(
            F("REPAIR-PROPOSER-LIFECYCLE-v1"), F(c.ProposerId), F(c.CredentialId),
            F(c.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.CredentialFingerprint), F(c.TransactionId),
            F(c.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.ExpectedStateFingerprint), F(c.ExpectedJournalHead ?? string.Empty),
            F(c.TargetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.TargetStateFingerprint), F(c.TargetJournalHead ?? string.Empty), F(c.CredentialVersion));
    }
}
