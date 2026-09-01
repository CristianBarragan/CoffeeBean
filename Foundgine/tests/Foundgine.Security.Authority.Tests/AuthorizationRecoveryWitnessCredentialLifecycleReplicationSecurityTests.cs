using Foundgine.Security.Authority;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Security.Authority.Tests;

public sealed class AuthorizationRecoveryWitnessCredentialLifecycleReplicationSecurityTests
{
    [Fact]
    public void Replica_applies_contiguous_rotation_and_revocation_history()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 3, AuthorizationRecoveryWitnessCredentialState.Revoked);

        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        foreach (var record in source.ReadAll())
            Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, replica.Apply(record));

        Assert.Equal(source.Revision, replica.Revision);
        Assert.Equal(source.HeadDigest, replica.HeadDigest);
    }

    [Fact]
    public void Duplicate_record_is_idempotent()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var record = source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, replica.Apply(record));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.AlreadyApplied, replica.Apply(record));
    }

    [Fact]
    public void Revision_gap_is_rejected()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);

        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var second = source.ReadAll()[1];

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Gap, replica.Apply(second));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Divergent_same_revision_is_rejected()
    {
        var sourceA = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var sourceB = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var first = sourceA.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var divergent = sourceB.Append("w1", "attacker-fp", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, replica.Apply(first));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.DivergentRevision, replica.Apply(divergent));
        Assert.Equal(first.Digest, replica.HeadDigest);
    }

    [Fact]
    public void Tampered_digest_is_rejected()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var record = source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var tampered = record with { CredentialFingerprint = "attacker" };
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidRecord, replica.Apply(tampered));
        Assert.Equal(0, replica.Revision);
    }

    [Fact]
    public void Tampered_previous_digest_is_rejected()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var record = source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        var tampered = record with { PreviousDigest = AuthorizationRecoveryWitnessCredentialLifecycleReplication.GenesisDigest };
        var replica = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, replica.Apply(source.ReadAll()[0]));

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidRecord, replica.Apply(tampered));
    }

    [Fact]
    public void Crash_recovery_converges_from_a_durable_history_package()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 3, AuthorizationRecoveryWitnessCredentialState.Revoked);
        var package = source.ExportRecoveryPackage();

        var recovered = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, recovered.Recover(package));
        Assert.Equal(source.Revision, recovered.Revision);
        Assert.Equal(source.HeadDigest, recovered.HeadDigest);
    }

    [Fact]
    public void Recovery_cannot_skip_history()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Active);
        var package = new AuthorizationRecoveryWitnessCredentialLifecycleRecoveryPackage(
            new[] { source.ReadAll()[1] }, source.HeadDigest);

        var recovered = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Gap, recovered.Recover(package));
        Assert.Equal(0, recovered.Revision);
    }

    [Fact]
    public void Recovery_cannot_resurrect_revoked_history()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        source.Append("w1", "fp-2", 2, AuthorizationRecoveryWitnessCredentialState.Revoked);
        var revoked = source.ExportRecoveryPackage();

        var recovered = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.Applied, recovered.Recover(revoked));

        var attacker = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        var stale = attacker.Append("w1", "fp-3", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.DivergentRevision, recovered.Apply(stale));
        Assert.Equal(AuthorizationRecoveryWitnessCredentialState.Revoked, recovered.ReadAll()[1].State);
    }

    [Fact]
    public void Recovery_package_head_mismatch_is_rejected()
    {
        var source = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();
        source.Append("w1", "fp-1", 1, AuthorizationRecoveryWitnessCredentialState.Active);
        var package = source.ExportRecoveryPackage() with { HeadDigest = "bad" };
        var recovered = new AuthorizationRecoveryWitnessCredentialLifecycleReplication();

        Assert.Equal(AuthorizationRecoveryWitnessCredentialLifecycleReplicationResult.InvalidHistory, recovered.Recover(package));
        Assert.Equal(0, recovered.Revision);
    }
}
