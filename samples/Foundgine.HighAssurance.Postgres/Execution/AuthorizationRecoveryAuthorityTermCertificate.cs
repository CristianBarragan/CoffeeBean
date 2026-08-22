using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>
/// Cryptographic evidence that an authority term was installed as the direct successor of
/// a specific predecessor. Secret key material is never serialized into the certificate.
/// </summary>
public sealed record AuthorizationRecoveryAuthorityTermCertificate(
    long PreviousTerm,
    string PreviousAuthorityId,
    long NewTerm,
    string NewAuthorityId,
    string PreviousCertificateDigest,
    string SigningKeyId,
    string Signature)
{
    public string CanonicalPayload => string.Join("|",
        "foundgine-authority-term/v1",
        PreviousTerm,
        PreviousAuthorityId,
        NewTerm,
        NewAuthorityId,
        PreviousCertificateDigest,
        SigningKeyId);

    public static AuthorizationRecoveryAuthorityTermCertificate Create(
        long previousTerm,
        string previousAuthorityId,
        long newTerm,
        string newAuthorityId,
        string previousCertificateDigest,
        string signingKeyId,
        ReadOnlySpan<byte> signingKey)
    {
        ValidateTransition(previousTerm, previousAuthorityId, newTerm, newAuthorityId, previousCertificateDigest, signingKeyId, signingKey);
        var payload = string.Join("|", "foundgine-authority-term/v1", previousTerm, previousAuthorityId,
            newTerm, newAuthorityId, previousCertificateDigest, signingKeyId);
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new AuthorizationRecoveryAuthorityTermCertificate(previousTerm, previousAuthorityId, newTerm,
            newAuthorityId, previousCertificateDigest.ToLowerInvariant(), signingKeyId, signature);
    }

    public bool Verify(ReadOnlySpan<byte> signingKey, AuthorizationRecoveryAuthorityState expectedCurrent)
    {
        if (PreviousTerm != expectedCurrent.Term ||
            !string.Equals(PreviousAuthorityId, expectedCurrent.AuthorityId, StringComparison.Ordinal) ||
            NewTerm != PreviousTerm + 1 || string.IsNullOrWhiteSpace(SigningKeyId))
            return false;

        byte[] expected;
        try
        {
            expected = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(CanonicalPayload));
            var supplied = Convert.FromHexString(Signature);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string Digest() => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalPayload + "|" + Signature))).ToLowerInvariant();

    private static void ValidateTransition(long previousTerm, string previousAuthorityId, long newTerm,
        string newAuthorityId, string previousCertificateDigest, string signingKeyId, ReadOnlySpan<byte> signingKey)
    {
        if (previousTerm < 1) throw new ArgumentOutOfRangeException(nameof(previousTerm));
        if (newTerm != previousTerm + 1) throw new ArgumentException("Authority terms must advance exactly one step.", nameof(newTerm));
        if (string.IsNullOrWhiteSpace(previousAuthorityId)) throw new ArgumentException("Previous authority identity is required.", nameof(previousAuthorityId));
        if (string.IsNullOrWhiteSpace(newAuthorityId)) throw new ArgumentException("New authority identity is required.", nameof(newAuthorityId));
        if (string.IsNullOrWhiteSpace(previousCertificateDigest)) throw new ArgumentException("Previous certificate digest is required.", nameof(previousCertificateDigest));
        if (string.IsNullOrWhiteSpace(signingKeyId)) throw new ArgumentException("Signing key identity is required.", nameof(signingKeyId));
        if (signingKey.IsEmpty) throw new ArgumentException("Signing key is required.", nameof(signingKey));
    }
}
