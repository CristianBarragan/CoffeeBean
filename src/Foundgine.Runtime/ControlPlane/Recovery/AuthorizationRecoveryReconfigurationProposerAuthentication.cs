using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Runtime.ControlPlane;

/// <summary>Credential presented by a control-plane proposer for witness-set reconfiguration.</summary>
public sealed record AuthorizationRecoveryReconfigurationProposerCredential(
    string ProposerId,
    string CredentialFingerprint,
    long ExpectedConfigVersion,
    string ProposedMembershipDigest,
    string CredentialVersion = "v1",
    long CredentialSequence = 1);

/// <summary>Authorizes a proposer for a specific expected configuration and proposed membership.</summary>
public interface IAuthorizationRecoveryReconfigurationProposerAuthorizer
{
    bool Authorize(
        AuthorizationRecoveryReconfigurationProposerCredential credential,
        long expectedConfigVersion,
        string proposedMembershipDigest);
}

/// <summary>Reference/test authorizer. Production credentials should come from an external control plane/KMS.</summary>
public sealed class FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer
    : IAuthorizationRecoveryReconfigurationProposerAuthorizer, IAuthorizationRecoveryReconfigurationProposerCredentialLifecycle
{
    private readonly IReadOnlyDictionary<string, string> _expectedFingerprints;
    private readonly AuthorizationRecoveryReconfigurationProposerCredentialLifecycle _lifecycle;

    public FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(
        IReadOnlyDictionary<string, string> expectedFingerprints,
        IAuthorizationRecoveryProposerCredentialRevocationStore? revocationStore = null)
    {
        _expectedFingerprints = expectedFingerprints ?? throw new ArgumentNullException(nameof(expectedFingerprints));
        _lifecycle = new AuthorizationRecoveryReconfigurationProposerCredentialLifecycle(revocationStore);
        foreach (var pair in _expectedFingerprints) _lifecycle.Register(pair.Key, pair.Value);
    }

    public bool Authorize(AuthorizationRecoveryReconfigurationProposerCredential credential, long expectedConfigVersion, string proposedMembershipDigest)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.ProposerId) || string.IsNullOrWhiteSpace(proposedMembershipDigest))
            return false;
        if (!string.Equals(credential.CredentialVersion, "v1", StringComparison.Ordinal))
            return false;
        if (credential.ExpectedConfigVersion != expectedConfigVersion ||
            !string.Equals(credential.ProposedMembershipDigest, proposedMembershipDigest, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!_expectedFingerprints.ContainsKey(credential.ProposerId))
            return false;
        var snapshot = _lifecycle.GetSnapshot(credential.ProposerId);
        // Compare against the lifecycle's current fingerprint, not the fingerprint the
        // proposer was registered with: Rotate() updates the lifecycle but never the
        // original _expectedFingerprints map, so that map is only a proposer allow-list.
        return snapshot.State == AuthorizationRecoveryReconfigurationProposerCredentialState.Active &&
               snapshot.CredentialSequence == credential.CredentialSequence &&
               FixedEquals(snapshot.CredentialFingerprint, credential.CredentialFingerprint);
    }


    public ValueTask<IAuthorizationRecoveryReconfigurationProposerCredentialLease?> TryAcquireAsync(AuthorizationRecoveryReconfigurationProposerCredential credential, CancellationToken cancellationToken = default) =>
        _lifecycle.TryAcquireAsync(credential, cancellationToken);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot GetSnapshot(string proposerId) =>
        _lifecycle.GetSnapshot(proposerId);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot RotateCredential(string proposerId, string newCredentialFingerprint) =>
        _lifecycle.Rotate(proposerId, newCredentialFingerprint);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot SetVerificationOnly(string proposerId) =>
        _lifecycle.SetVerificationOnly(proposerId);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot RetireCredential(string proposerId) =>
        _lifecycle.Retire(proposerId);

    public AuthorizationRecoveryReconfigurationProposerCredentialLifecycleSnapshot RevokeCredential(string proposerId) =>
        _lifecycle.Revoke(proposerId);

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}

/// <summary>Explicit fail-closed default: reconfiguration cannot proceed without an authenticated proposer.</summary>
public sealed class DenyAllAuthorizationRecoveryReconfigurationProposerAuthorizer
    : IAuthorizationRecoveryReconfigurationProposerAuthorizer
{
    public static readonly DenyAllAuthorizationRecoveryReconfigurationProposerAuthorizer Instance = new();
    private DenyAllAuthorizationRecoveryReconfigurationProposerAuthorizer() { }
    public bool Authorize(AuthorizationRecoveryReconfigurationProposerCredential credential, long expectedConfigVersion, string proposedMembershipDigest) => false;
}
