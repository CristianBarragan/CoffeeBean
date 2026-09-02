using Foundgine.Runtime.ControlPlane;
using Xunit;

public sealed class AuthorizationRecoveryControlPlaneTransactionJournalIntegrityTests
{
    private static readonly byte[] JournalKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 10)).ToArray();

    [Fact]
    public void Prepared_then_committed_chain_verifies()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.Accepted,
            journal.Append("tx-1", 7, 8, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a"));
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.Accepted,
            journal.Append("tx-1", 7, 8, "historical-recovery", AuthorizationRecoveryDurableCommitPhase.Committed, "fp-a"));
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.Accepted, journal.VerifyChain());
    }

    [Fact]
    public void Tampered_digest_is_rejected()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        journal.Append("tx-1", 1, 2, "retirement", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        var tampered = journal.Entries[0] with { TargetFingerprint = "forged" };
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.RejectedDigest, journal.VerifyEntry(tampered));
    }

    [Fact]
    public void Tampered_authentication_tag_is_rejected()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        journal.Append("tx-1", 1, 2, "publication", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        var tampered = journal.Entries[0] with { AuthenticationTag = new string('0', 64) };
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.RejectedAuthentication, journal.VerifyEntry(tampered));
    }

    [Fact]
    public void Journal_entry_sequence_tampering_is_rejected()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        journal.Append("tx-1", 1, 2, "publication", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        var forged = journal.Entries[0] with { JournalSequence = 3 };
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.RejectedDigest, journal.VerifyEntry(forged));
    }

    [Fact]
    public void Replay_with_changed_fingerprint_is_rejected()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        journal.Append("tx-1", 1, 2, "publication", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        Assert.Equal(AuthorizationRecoveryTransactionJournalResult.RejectedReplay,
            journal.Append("tx-1", 1, 2, "publication", AuthorizationRecoveryDurableCommitPhase.Committed, "forged-fp"));
    }

    [Fact]
    public void Wrong_journal_key_cannot_verify_entry()
    {
        var journal = new AuthorizationRecoveryControlPlaneTransactionJournal(JournalKey);
        journal.Append("tx-1", 1, 2, "recovery", AuthorizationRecoveryDurableCommitPhase.Prepared, "fp-a");
        var entry = journal.Entries[0];
        Assert.False(AuthorizationRecoveryControlPlaneTransactionJournalIntegrity.VerifyEntry(entry, new byte[32]));
    }
}
