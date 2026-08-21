using System.Collections.Concurrent;
using System.Security.Cryptography;
using Foundgine.Authorization;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrityTests
{
    private static readonly byte[] ReplicationKey = SHA256.HashData("m5.72-replication-key"u8.ToArray());

    private static AuthorizationRecoveryRepairProposerCredentialDurableLifecycle State(
        long sequence,
        AuthorizationRecoveryRepairProposerCredentialState state = AuthorizationRecoveryRepairProposerCredentialState.Active,
        string credentialId = "cred-v1",
        string fingerprint = "fp-v1") =>
        new("operator-a", credentialId, fingerprint, sequence, state);

    private static AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity Pair(
        string instance = "instance-b", long epoch = 7) =>
        new(instance, epoch, ReplicationKey);

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
            sameSequenceDifferentState.SourceInstanceId, sameSequenceDifferentState.PreviousSequence.ToString(),
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
        var target = new AuthorizationRecoveryControlPlaneRepairProposerCredentialReplicationIntegrity("instance-b", 7, SHA256.HashData("wrong-key"u8.ToArray()));
        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.InvalidIntegrity, target.Apply(source.CreateEnvelope(State(1))));
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
            forgedNewEpoch.State.ToString(), forgedNewEpoch.AuthorityEpoch.ToString(), forgedNewEpoch.SourceInstanceId,
            forgedNewEpoch.PreviousSequence.ToString(), forgedNewEpoch.PreviousDigest, forgedNewEpoch.StateDigest);
        forgedNewEpoch = forgedNewEpoch with { IntegrityProof = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))) };

        Assert.Equal(AuthorizationRecoveryRepairProposerCredentialReplicationApplyResult.DivergentState, target.Apply(forgedNewEpoch));
    }
}
