using Foundgine.Authorization;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Authorization.Tests;

public sealed class AuthorizationRecoveryProposerCredentialAuditLedgerSecurityTests
{
    [Fact]
    public void Accepted_transitions_are_hash_chained_and_verify()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        ledger.Append("operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        ledger.Append("operator-a", "fp-v2", 2, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch.AddSeconds(1));
        ledger.Append("operator-a", "fp-v2", 3, AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked, DateTimeOffset.UnixEpoch.AddSeconds(2));
        ledger.Append("operator-b", "fp-b1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch.AddSeconds(3));
        Assert.True(ledger.VerifyChain().Verified);
        Assert.True(AuthorizationRecoveryProposerCredentialAuditLedger.Restore(ledger.Records).VerifyChain().Verified);
    }

    [Fact]
    public void Edited_record_is_detected()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        ledger.Append("operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var records = ledger.Records.ToArray();
        records[0] = records[0] with { State = AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked };
        Assert.Equal(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.RecordDigestMismatch, AuthorizationRecoveryProposerCredentialAuditLedger.VerifyChain(records).Outcome);
    }

    [Fact]
    public void Deleted_or_reordered_record_is_detected()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        ledger.Append("a", "a1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        ledger.Append("b", "b1", 2, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch.AddSeconds(1));
        ledger.Append("c", "c1", 3, AuthorizationRecoveryReconfigurationProposerCredentialState.Revoked, DateTimeOffset.UnixEpoch.AddSeconds(2));
        var records = ledger.Records.ToArray();
        var deleted = new[] { records[0], records[2] };
        var reordered = new[] { records[0], records[2], records[1] };
        Assert.Equal(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.SequenceGap, AuthorizationRecoveryProposerCredentialAuditLedger.VerifyChain(deleted).Outcome);
        Assert.NotEqual(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.Verified, AuthorizationRecoveryProposerCredentialAuditLedger.VerifyChain(reordered).Outcome);
    }

    [Fact]
    public void Forged_record_digest_is_detected()
    {
        var ledger = new AuthorizationRecoveryProposerCredentialAuditLedger();
        ledger.Append("operator-a", "fp-v1", 1, AuthorizationRecoveryReconfigurationProposerCredentialState.Active, DateTimeOffset.UnixEpoch);
        var r = ledger.Records[0];
        var forged = new[] { r with { RecordDigest = new string('0', r.RecordDigest.Length) } };
        Assert.Equal(AuthorizationRecoveryProposerCredentialAuditVerificationOutcome.RecordDigestMismatch, AuthorizationRecoveryProposerCredentialAuditLedger.VerifyChain(forged).Outcome);
    }
}
