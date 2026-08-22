using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>
/// Independent witness attestation for an authority-term certificate. Witnesses attest to the
/// exact certificate digest; they never supply authority and they never carry secret material in
/// the attestation. A certificate is accepted only when a configured majority independently signs
/// the same digest.
/// </summary>
public sealed record AuthorizationRecoveryAuthorityTermWitnessSignature(
    string WitnessId,
    string Signature);

public sealed record AuthorizationRecoveryAuthorityTermQuorumAttestation(
    string CertificateDigest,
    IReadOnlyList<AuthorizationRecoveryAuthorityTermWitnessSignature> WitnessSignatures)
{
    public static AuthorizationRecoveryAuthorityTermQuorumAttestation Create(
        AuthorizationRecoveryAuthorityTermCertificate certificate,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> witnessKeys,
        IReadOnlyList<string> witnessIds)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));
        if (witnessKeys is null) throw new ArgumentNullException(nameof(witnessKeys));
        if (witnessIds is null || witnessIds.Count == 0) throw new ArgumentException("At least one witness is required.", nameof(witnessIds));

        var digest = certificate.Digest();
        var signatures = new List<AuthorizationRecoveryAuthorityTermWitnessSignature>();
        foreach (var witnessId in witnessIds.Distinct(StringComparer.Ordinal))
        {
            if (!witnessKeys.TryGetValue(witnessId, out var key) || key.IsEmpty)
                throw new ArgumentException($"No signing key is provisioned for witness '{witnessId}'.", nameof(witnessKeys));
            signatures.Add(new AuthorizationRecoveryAuthorityTermWitnessSignature(
                witnessId,
                Sign(witnessId, digest, key.Span)));
        }

        return new AuthorizationRecoveryAuthorityTermQuorumAttestation(digest, signatures);
    }

    public bool Verify(
        AuthorizationRecoveryAuthorityTermCertificate certificate,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> witnessKeys,
        IReadOnlyCollection<string> configuredWitnessIds)
    {
        if (certificate is null || witnessKeys is null || configuredWitnessIds is null)
            return false;

        var expectedDigest = certificate.Digest();
        if (!string.Equals(CertificateDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
            return false;

        var configured = configuredWitnessIds.ToHashSet(StringComparer.Ordinal);
        var unique = WitnessSignatures
            .GroupBy(static s => s.WitnessId, StringComparer.Ordinal)
            .Select(static g => g.First())
            .ToArray();
        if (unique.Length != WitnessSignatures.Count || unique.Length == 0)
            return false;

        var valid = 0;
        foreach (var attestation in unique)
        {
            if (!configured.Contains(attestation.WitnessId) ||
                !witnessKeys.TryGetValue(attestation.WitnessId, out var key) || key.IsEmpty)
                continue;

            if (VerifySignature(attestation.WitnessId, expectedDigest, attestation.Signature, key.Span))
                valid++;
        }

        var required = (configured.Count / 2) + 1;
        return valid >= required;
    }

    private static string Sign(string witnessId, string certificateDigest, ReadOnlySpan<byte> key) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalPayload(witnessId, certificateDigest))))
            .ToLowerInvariant();

    private static bool VerifySignature(string witnessId, string certificateDigest, string signature, ReadOnlySpan<byte> key)
    {
        try
        {
            var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalPayload(witnessId, certificateDigest)));
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CanonicalPayload(string witnessId, string certificateDigest) =>
        string.Join("|", "foundgine-authority-term-witness/v1", witnessId, certificateDigest);
}

/// <summary>
/// Verification boundary separating certificate validity from quorum validity. A valid predecessor
/// certificate proves succession; this verifier additionally requires independent witness majority.
/// </summary>
public sealed class AuthorizationRecoveryAuthorityTermQuorumVerifier
{
    private readonly IReadOnlyCollection<string> _witnessIds;
    private readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> _witnessKeys;

    public AuthorizationRecoveryAuthorityTermQuorumVerifier(
        IReadOnlyCollection<string> witnessIds,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> witnessKeys)
    {
        _witnessIds = witnessIds ?? throw new ArgumentNullException(nameof(witnessIds));
        _witnessKeys = witnessKeys ?? throw new ArgumentNullException(nameof(witnessKeys));
        if (_witnessIds.Count == 0) throw new ArgumentException("At least one witness is required.", nameof(witnessIds));
        if (_witnessIds.Distinct(StringComparer.Ordinal).Count() != _witnessIds.Count)
            throw new ArgumentException("Witness ids must be unique.", nameof(witnessIds));
    }

    public bool Verify(
        AuthorizationRecoveryAuthorityTermCertificate certificate,
        AuthorizationRecoveryAuthorityTermQuorumAttestation attestation,
        AuthorizationRecoveryAuthorityState expectedCurrent,
        ReadOnlySpan<byte> predecessorKey)
    {
        return certificate is not null &&
               attestation is not null &&
               certificate.Verify(predecessorKey, expectedCurrent) &&
               attestation.Verify(certificate, _witnessKeys, _witnessIds);
    }
}
