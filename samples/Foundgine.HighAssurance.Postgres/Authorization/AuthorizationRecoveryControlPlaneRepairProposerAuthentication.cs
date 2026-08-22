using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

public enum AuthorizationRecoveryRepairProposerCredentialState
{
    Active,
    VerificationOnly,
    Retired,
    Revoked
}

public sealed record AuthorizationRecoveryRepairProposerCredential(
    string ProposerId,
    string CredentialId,
    long CredentialSequence,
    string CredentialFingerprint,
    string TransactionId,
    long ExpectedRevision,
    string ExpectedStateFingerprint,
    string ExpectedJournalHead,
    long TargetRevision,
    string TargetStateFingerprint,
    string TargetJournalHead,
    string Proof,
    string CredentialVersion = "v1");

public enum AuthorizationRecoveryRepairProposerAuthorizationResult
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
/// M5.69 binds repair authorization to the exact repair transaction and
/// durable state transition. A valid proposer credential cannot be replayed
/// against another repair plan or another durable state.
/// </summary>
public sealed class AuthorizationRecoveryControlPlaneRepairProposerAuthentication
{
    private sealed record Registration(
        string Fingerprint,
        long Sequence,
        AuthorizationRecoveryRepairProposerCredentialState State,
        byte[] ProofKey);

    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _proposers = new(StringComparer.Ordinal);

    public void Register(string proposerId, string credentialFingerprint, ReadOnlySpan<byte> proofKey, long sequence = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialFingerprint);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (proofKey.Length < 16) throw new ArgumentException("Proof key must be at least 128 bits.", nameof(proofKey));
        lock (_gate) _proposers[proposerId] = new(credentialFingerprint, sequence, AuthorizationRecoveryRepairProposerCredentialState.Active, proofKey.ToArray());
    }

    public void SetState(string proposerId, AuthorizationRecoveryRepairProposerCredentialState state)
    {
        lock (_gate)
        {
            if (!_proposers.TryGetValue(proposerId, out var current)) throw new KeyNotFoundException(proposerId);
            _proposers[proposerId] = current with { State = state };
        }
    }

    public AuthorizationRecoveryRepairProposerAuthorizationResult Authorize(
        AuthorizationRecoveryRepairProposerCredential credential)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.ProposerId) ||
            string.IsNullOrWhiteSpace(credential.CredentialId) || credential.CredentialSequence < 1 ||
            string.IsNullOrWhiteSpace(credential.TransactionId) || string.IsNullOrWhiteSpace(credential.ExpectedStateFingerprint) ||
            string.IsNullOrWhiteSpace(credential.TargetStateFingerprint))
            return AuthorizationRecoveryRepairProposerAuthorizationResult.InvalidCredential;

        Registration registration;
        lock (_gate)
        {
            if (!_proposers.TryGetValue(credential.ProposerId, out registration!))
                return AuthorizationRecoveryRepairProposerAuthorizationResult.UnknownProposer;
        }

        if (registration.State != AuthorizationRecoveryRepairProposerCredentialState.Active)
            return AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialNotActive;
        if (!string.Equals(credential.CredentialVersion, "v1", StringComparison.Ordinal))
            return AuthorizationRecoveryRepairProposerAuthorizationResult.InvalidCredential;
        if (credential.CredentialSequence != registration.Sequence)
            return AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialSequenceMismatch;
        if (!FixedEquals(credential.CredentialFingerprint, registration.Fingerprint))
            return AuthorizationRecoveryRepairProposerAuthorizationResult.CredentialFingerprintMismatch;
        if (credential.TargetRevision != credential.ExpectedRevision + 1)
            return AuthorizationRecoveryRepairProposerAuthorizationResult.StateBindingMismatch;

        var proofPayload = Canonicalize(credential);
        using var hmac = new HMACSHA256(registration.ProofKey);
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(proofPayload));
        byte[] supplied;
        try { supplied = Convert.FromHexString(credential.Proof); }
        catch (FormatException) { return AuthorizationRecoveryRepairProposerAuthorizationResult.ProofMismatch; }
        return CryptographicOperations.FixedTimeEquals(supplied, expected)
            ? AuthorizationRecoveryRepairProposerAuthorizationResult.Authorized
            : AuthorizationRecoveryRepairProposerAuthorizationResult.ProofMismatch;
    }

    public static string CreateProof(AuthorizationRecoveryRepairProposerCredential credential, ReadOnlySpan<byte> proofKey)
    {
        using var hmac = new HMACSHA256(proofKey.ToArray());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonicalize(credential))));
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static string Canonicalize(AuthorizationRecoveryRepairProposerCredential c)
    {
        static string F(string value) => $"{Encoding.UTF8.GetByteCount(value)}:{value}";
        return string.Concat(
            F("REPAIR-PROPOSER-AUTH-v1"), F(c.ProposerId), F(c.CredentialId),
            F(c.CredentialSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.CredentialFingerprint), F(c.TransactionId),
            F(c.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.ExpectedStateFingerprint), F(c.ExpectedJournalHead ?? string.Empty),
            F(c.TargetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            F(c.TargetStateFingerprint), F(c.TargetJournalHead ?? string.Empty), F(c.CredentialVersion));
    }
}
