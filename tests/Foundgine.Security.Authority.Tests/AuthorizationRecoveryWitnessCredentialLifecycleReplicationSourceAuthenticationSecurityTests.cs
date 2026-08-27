using System.Text;
using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthenticationSecurityTests
{
    private static byte[] Key(string value) => Encoding.UTF8.GetBytes(value.PadRight(32, 'x'));

    [Fact]
    public void Authenticated_source_can_publish_contiguous_lifecycle_history()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));

        var first = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var second = source.AppendAndCreateEnvelope("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        var revoked = source.AppendAndCreateEnvelope("w1", "fp-2", 3, AuthorizationRecoveryWitnessCredentialState.Revoked);

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(first));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(second));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(revoked));
        Assert.Equal(source.Revision, replica.Revision);
        Assert.Equal(source.HeadDigest, replica.HeadDigest);
    }

    [Fact]
    public void Untrusted_source_is_rejected_before_lifecycle_mutation()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.UntrustedSource, replica.Apply(envelope));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Source_identity_spoofing_invalidates_the_proof()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        replica.TrustSourceInstance("attacker", Key("attacker"));

        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var spoofed = envelope with { SourceInstanceId = "attacker" };

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidIntegrity, replica.Apply(spoofed));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Tampering_with_the_lifecycle_record_is_rejected_by_source_authentication()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var tampered = envelope with
        {
            Record = envelope.Record with { State = AuthorizationRecoveryWitnessCredentialState.Revoked }
        };

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidIntegrity, replica.Apply(tampered));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Tampering_with_source_key_id_is_rejected()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        replica.TrustSourceKey("source-a", "other-key", 1, Key("other-key"));
        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);

        var tampered = envelope with { SourceKeyId = "other-key" };
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidIntegrity, replica.Apply(tampered));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Revoked_source_key_is_rejected_even_when_the_proof_is_valid()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        Assert.Equal(
            AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Activated,
            replica.RotateTrustedSourceKey("source-a", "witness-source-key-v1", "source-key-v2", 2, Key("source-v2")));
        Assert.Equal(
            AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Revoked,
            replica.RevokeTrustedSourceKey("source-a", "witness-source-key-v1"));

        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.RevokedSourceKey, replica.Apply(envelope));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Verification_only_source_key_remains_valid_during_rotation_overlap()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));

        var first = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(first));

        Assert.Equal(
            AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Activated,
            source.RotateLocalSourceKey("witness-source-key-v1", "witness-source-key-v2", 2, Key("source-v2")));
        Assert.Equal(
            AuthorizationRecoveryWitnessSourceTrustKeyLifecycleResult.Activated,
            replica.RotateTrustedSourceKey("source-a", "witness-source-key-v1", "witness-source-key-v2", 2, Key("source-v2")));

        var second = source.AppendAndCreateEnvelope("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(second));
    }

    [Fact]
    public void Duplicate_authenticated_record_is_idempotent()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        var envelope = source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied, replica.Apply(envelope));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.AlreadyApplied, replica.Apply(envelope));
        Assert.Equal(1, replica.Revision);
    }

    [Fact]
    public void Authenticated_revision_gap_still_fails_closed()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var second = source.AppendAndCreateEnvelope("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Gap, replica.Apply(second));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Authenticated_recovery_verifies_every_source_proof_before_commit()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.AppendAndCreateEnvelope("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Revoked);
        var history = source.ExportAuthenticatedHistory().ToArray();
        history[1] = history[1] with { IntegrityProof = "forged" };

        Assert.Equal(
            AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.InvalidIntegrity,
            replica.Recover(history, source.HeadDigest));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Authenticated_recovery_preserves_revocation_and_cannot_be_replaced_by_stale_source_history()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("replica", Key("replica"));
        replica.TrustSourceInstance("source-a", Key("source-a"));
        source.AppendAndCreateEnvelope("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.AppendAndCreateEnvelope("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Revoked);

        Assert.Equal(
            AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.Applied,
            replica.Recover(source.ExportAuthenticatedHistory(), source.HeadDigest));

        var stale = new AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceAuthentication("source-a", Key("source-a"));
        var staleEnvelope = stale.AppendAndCreateEnvelope("w1", "fp-3", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.DivergentRevision, replica.Apply(staleEnvelope));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationSourceApplyResult.AlreadyApplied,
            replica.Apply(source.ExportAuthenticatedHistory()[0]));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialState.Revoked, replica.ReadAll()[1].State);
    }
}

