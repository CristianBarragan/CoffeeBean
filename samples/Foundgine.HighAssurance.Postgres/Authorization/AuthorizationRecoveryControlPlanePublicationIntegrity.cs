using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Authorization;

/// <summary>
/// Cryptographically authenticated control-plane publication.
/// The tag binds authority epoch, owner, authorization sequence, history head,
/// and integrity-key identity into one canonical representation.
/// </summary>
public sealed record AuthorizationRecoveryControlPlanePublication(
    long Epoch,
    string ActiveControlPlaneId,
    long Sequence,
    string HeadDigest,
    string IntegrityKeyId,
    string AlgorithmVersion,
    string Tag);

public static class AuthorizationRecoveryControlPlanePublicationIntegrity
{
    public const string SupportedAlgorithm = "HMAC-SHA256/v1";

    public static string ComputeTag(
        long epoch,
        string activeControlPlaneId,
        long sequence,
        string headDigest,
        string integrityKeyId,
        ReadOnlySpan<byte> key)
    {
        var canonical = Canonicalize(
            epoch, activeControlPlaneId, sequence, headDigest,
            integrityKeyId, SupportedAlgorithm);

        using var hmac = new HMACSHA256(key.ToArray());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    public static bool Verify(
        AuthorizationRecoveryControlPlanePublication publication,
        ReadOnlySpan<byte> key)
    {
        if (!string.Equals(publication.AlgorithmVersion, SupportedAlgorithm, StringComparison.Ordinal))
            return false;

        var expected = ComputeTag(
            publication.Epoch,
            publication.ActiveControlPlaneId,
            publication.Sequence,
            publication.HeadDigest,
            publication.IntegrityKeyId,
            key);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(publication.Tag),
            Convert.FromHexString(expected));
    }

    private static string Canonicalize(
        long epoch,
        string owner,
        long sequence,
        string headDigest,
        string keyId,
        string algorithm)
    {
        static string Field(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return $"{bytes.Length}:{value}";
        }

        return string.Concat(
            Field("authorization-recovery-control-plane-publication"),
            Field(epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field(owner),
            Field(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field(headDigest),
            Field(keyId),
            Field(algorithm));
    }
}
