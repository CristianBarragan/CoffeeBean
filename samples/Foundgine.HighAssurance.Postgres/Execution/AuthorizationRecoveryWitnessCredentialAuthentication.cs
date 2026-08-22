using System.Security.Cryptography;
using System.Text;

namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>Opaque credential presented by a recovery witness during bootstrap/reconciliation.</summary>
public sealed record AuthorizationRecoveryWitnessCredential(
    string WitnessId,
    string CredentialFingerprint,
    string CredentialVersion = "v1");

/// <summary>
/// Verifies a witness credential without persisting the credential secret in the recovery ledger.
/// Implementations should obtain credential material from an external secret/KMS boundary.
/// </summary>
public interface IAuthorizationRecoveryWitnessCredentialAuthenticator
{
    bool Authenticate(
        string witnessId,
        AuthorizationRecoveryWitnessCredential credential);
}

public sealed class AuthorizationRecoveryWitnessCredentialAuthenticationException : Exception
{
    public AuthorizationRecoveryWitnessCredentialAuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Reference verifier for tests. The verifier compares a canonical fingerprint rather than the
/// credential secret itself, and binds the fingerprint to witness identity and credential version.
/// </summary>
public sealed class FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator
    : IAuthorizationRecoveryWitnessCredentialAuthenticator
{
    private readonly IReadOnlyDictionary<string, string> _expectedFingerprints;

    public FingerprintAuthorizationRecoveryWitnessCredentialAuthenticator(
        IReadOnlyDictionary<string, string> expectedFingerprints)
    {
        _expectedFingerprints = expectedFingerprints ?? throw new ArgumentNullException(nameof(expectedFingerprints));
    }

    public bool Authenticate(string witnessId, AuthorizationRecoveryWitnessCredential credential)
    {
        if (string.IsNullOrWhiteSpace(witnessId) || credential is null)
            return false;
        if (!string.Equals(witnessId, credential.WitnessId, StringComparison.Ordinal))
            return false;
        if (!string.Equals(credential.CredentialVersion, "v1", StringComparison.Ordinal))
            return false;
        if (!_expectedFingerprints.TryGetValue(witnessId, out var expected))
            return false;
        return FixedEquals(expected, credential.CredentialFingerprint);
    }

    private static bool FixedEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Resolver decorator that requires an authenticated credential before a durable witness id can
/// become a live witness handle. A matching id alone is never sufficient.
/// </summary>
public sealed class AuthenticatedAuthorizationRecoveryWitnessResolver
{
    private readonly IAuthorizationRecoveryWitnessResolver _inner;
    private readonly IAuthorizationRecoveryWitnessCredentialAuthenticator _authenticator;

    public AuthenticatedAuthorizationRecoveryWitnessResolver(
        IAuthorizationRecoveryWitnessResolver inner,
        IAuthorizationRecoveryWitnessCredentialAuthenticator authenticator)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }

    public IReadOnlyList<AuthorizationRecoveryQuorumWitness> Resolve(
        IReadOnlyList<AuthorizationRecoveryWitnessCredential> credentials)
    {
        if (credentials is null || credentials.Count == 0)
            throw new AuthorizationRecoveryWitnessCredentialAuthenticationException("Authenticated witness credentials are required.");

        var ids = credentials.Select(static c => c.WitnessId).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new AuthorizationRecoveryWitnessCredentialAuthenticationException("Witness credentials must contain unique, non-empty witness identities.");

        foreach (var credential in credentials)
        {
            if (!_authenticator.Authenticate(credential.WitnessId, credential))
                throw new AuthorizationRecoveryWitnessCredentialAuthenticationException(
                    $"Witness credential authentication failed for '{credential.WitnessId}'.");
        }

        var witnesses = _inner.Resolve(ids);
        var byId = witnesses.ToDictionary(static w => w.WitnessId, StringComparer.Ordinal);
        if (byId.Count != ids.Length || ids.Any(id => !byId.ContainsKey(id)))
            throw new AuthorizationRecoveryWitnessCredentialAuthenticationException(
                "Authenticated witness identities could not be resolved exactly to the durable membership.");

        return ids.Select(id => byId[id]).ToArray();
    }
}
