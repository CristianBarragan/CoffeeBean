using System.Collections.Concurrent;
using System.Security.Cryptography;
using Foundgine.Runtime.ControlPlane;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrityTests
{
    private static readonly byte[] ReplicationKey = SHA256.HashData("m5.74-replication-key-v1"u8.ToArray());
    private static readonly byte[] RotationKey = SHA256.HashData("m5.74-replication-key-v2"u8.ToArray());

    private static AuthorizationRecoveryRepairProposerCredentialDurableLifecycle State(
        long sequence,
        AuthorizationRecoveryRepairProposerCredentialState state = AuthorizationRecoveryRepairProposerCredentialState.Active,
        string credentialId = "cred-v1",
        string fingerprint = "fp-v1") =>
        new("operator-a", credentialId, fingerprint, sequence, state);

    private static AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity Pair(
        string instance = "instance-b", long epoch = 7)
    {
        var target = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity(instance, epoch, ReplicationKey);
        target.TrustSourceInstance("instance-a", ReplicationKey);
        return target;
    }

    [Fact]
    public void Tampered_envelope_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var envelope = source.CreateEnvelope(State(1));

        var tampered = envelope with { CredentialFingerprint = "forged" };
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.InvalidIntegrity, target.Apply(tampered));
    }

    [Fact]
    public void Sequence_gap_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        var gap = source.CreateEnvelope(State(3), first.StateDigest);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.SequenceGap, target.Apply(gap));
    }

    [Fact]
    public void Previous_digest_mismatch_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        var second = source.CreateEnvelope(State(2), "WRONG");
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.PreviousDigestMismatch, target.Apply(second));
    }

    [Fact]
    public void Duplicate_is_idempotent_but_divergent_same_sequence_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Duplicate, target.Apply(first));

        var divergent = source.CreateEnvelope(State(2));
        var sameSequenceDifferentState = divergent with { CredentialSequence = 1, StateDigest = "different" };
        using var hmac = new HMACSHA256(ReplicationKey);
        // Re-sign the intentionally divergent envelope so this test reaches the fork check.
        var canonical = string.Join("|", sameSequenceDifferentState.ProposerId, sameSequenceDifferentState.CredentialId,
            sameSequenceDifferentState.CredentialFingerprint, sameSequenceDifferentState.CredentialSequence.ToString(),
            sameSequenceDifferentState.State.ToString(), sameSequenceDifferentState.AuthorityEpoch.ToString(),
            sameSequenceDifferentState.SourceInstanceId, sameSequenceDifferentState.SourceKeyId, sameSequenceDifferentState.PreviousSequence.ToString(),
            sameSequenceDifferentState.PreviousDigest, sameSequenceDifferentState.StateDigest);
        var resigned = sameSequenceDifferentState with { IntegrityProof = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))) };
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState, target.Apply(resigned));
    }

    [Fact]
    public void Rollback_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        var second = source.CreateEnvelope(State(2), first.StateDigest);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(second));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.SequenceRollback, target.Apply(first));
    }

    [Fact]
    public void Old_authority_epoch_is_rejected_after_promotion()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        target.PromoteAuthority(8);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.AuthorityEpochMismatch, target.Apply(first));
    }

    [Fact]
    public void Wrong_replication_key_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-b", 7, SHA256.HashData("wrong-target-key"u8.ToArray()));
        target.TrustSourceInstance("instance-a", SHA256.HashData("wrong-key"u8.ToArray()));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.InvalidIntegrity, target.Apply(source.CreateEnvelope(State(1))));
    }

    [Fact]
    public void Untrusted_source_is_rejected_before_integrity_is_evaluated()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-b", 7, ReplicationKey);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.UntrustedSource, target.Apply(source.CreateEnvelope(State(1))));
    }

    [Fact]
    public void Source_identity_spoofing_is_rejected_even_when_attacker_reuses_a_trusted_source_key()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var envelope = source.CreateEnvelope(State(1));
        var spoofed = envelope with { SourceInstanceId = "instance-c" };
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.UntrustedSource, target.Apply(spoofed));
    }

    [Fact]
    public async Task Thirty_two_concurrent_replication_attempts_allow_only_one_next_sequence()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        var results = new ConcurrentBag<AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult>();
        var envelopes = Enumerable.Range(0, 32).Select(i =>
        {
            var candidate = State(2, credentialId: $"cred-{i}", fingerprint: $"fp-{i}");
            return source.CreateEnvelope(candidate, first.StateDigest);
        }).ToArray();

        await Task.WhenAll(envelopes.Select(envelope => Task.Run(() => results.Add(target.Apply(envelope)))));

        Assert.Equal(1, results.Count(r => r == AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied));
        Assert.Equal(31, results.Count(r => r == AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState));
    }

    [Fact]
    public void Same_epoch_state_cannot_be_resurrected_after_authority_promotion()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));
        target.PromoteAuthority(8);

        var forgedNewEpoch = first with { AuthorityEpoch = 8 };
        using var hmac = new HMACSHA256(ReplicationKey);
        var canonical = string.Join("|", forgedNewEpoch.ProposerId, forgedNewEpoch.CredentialId,
            forgedNewEpoch.CredentialFingerprint, forgedNewEpoch.CredentialSequence.ToString(),
            forgedNewEpoch.State.ToString(), forgedNewEpoch.AuthorityEpoch.ToString(), forgedNewEpoch.SourceInstanceId, forgedNewEpoch.SourceKeyId,
            forgedNewEpoch.PreviousSequence.ToString(), forgedNewEpoch.PreviousDigest, forgedNewEpoch.StateDigest);
        forgedNewEpoch = forgedNewEpoch with { IntegrityProof = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))) };

        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState, target.Apply(forgedNewEpoch));
    }
    [Fact]
    public void Trusted_source_key_rotation_accepts_in_flight_old_key_and_new_key()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated,
            target.RotateTrustedSourceKey("instance-a", "source-key-v1", "source-key-v2", 2, RotationKey));

        var oldKeyMessage = source.CreateEnvelope(State(2), first.StateDigest);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(oldKeyMessage));

        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated,
            source.RotateLocalSourceKey("source-key-v1", "source-key-v2", 2, RotationKey));
        var newKeyMessage = source.CreateEnvelope(State(3), oldKeyMessage.StateDigest);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(newKeyMessage));
    }

    [Fact]
    public void Revoked_source_key_is_rejected_even_when_message_integrity_is_valid()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var first = source.CreateEnvelope(State(1));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.Applied, target.Apply(first));

        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated,
            target.RotateTrustedSourceKey("instance-a", "source-key-v1", "source-key-v2", 2, RotationKey));
        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.Revoked,
            target.RevokeTrustedSourceKey("instance-a", "source-key-v1"));

        var replay = source.CreateEnvelope(State(2), first.StateDigest);
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.RevokedSourceKey, target.Apply(replay));
    }

    [Fact]
    public void Unknown_source_key_is_rejected()
    {
        var source = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-a", 7, ReplicationKey);
        var target = Pair();
        var envelope = source.CreateEnvelope(State(1)) with { SourceKeyId = "source-key-unknown" };
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.UnknownSourceKey, target.Apply(envelope));
    }

    [Fact]
    public void Stale_trusted_source_rotation_is_rejected()
    {
        var target = Pair();
        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.Activated,
            target.RotateTrustedSourceKey("instance-a", "source-key-v1", "source-key-v2", 2, RotationKey));
        Assert.Equal(AuthorizationRecoverySourceTrustKeyLifecycleResult.StaleRotation,
            target.RotateTrustedSourceKey("instance-a", "source-key-v1", "source-key-v3", 3, RotationKey));
    }

}
