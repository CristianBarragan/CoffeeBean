using Foundgine.Authorization;
using Xunit;

public sealed partial class AuthorizationRecoveryControlPlaneCommitReconciliationTests
{
    private static readonly byte[] KeyV1 = new byte[32];
    private static readonly byte[] KeyV2 = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();

    private static AuthorizationRecoveryControlPlanePublication Publication(string keyId, byte[] key, long sequence) =>
        new(
            9,
            "primary",
            sequence,
            $"digest-{sequence}",
            keyId,
            AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
            AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
                9, "primary", sequence, $"digest-{sequence}", keyId, key));

    private static AuthorizationRecoveryControlPlaneCommitReconciliation Create(long window = 0, long sequence = 43) =>
        new(
            new AuthorizationRecoveryCrossInstanceState(
                1,
                new AuthorizationRecoveryKeyRing(
                    "key-v2",
                    new Dictionary<string, AuthorizationRecoveryIntegrityKey>
                    {
                        ["key-v1"] = new("key-v1", AuthorizationRecoveryKeyStatus.VerificationOnly, 1),
                        ["key-v2"] = new("key-v2", AuthorizationRecoveryKeyStatus.Active, 2)
                    }),
                Publication("key-v2", KeyV2, sequence)),
            window,
            keyId => keyId switch
            {
                "key-v1" => KeyV1,
                "key-v2" => KeyV2,
                _ => null
            });

    [Fact]
    public void Crash_after_prepare_before_apply_is_discarded_and_state_is_unchanged()
    {
        var store = Create();
        var before = store.Current.State;
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareHistoricalRecovery(historical, "tx-1", out _));

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.ExecutePreparedRecovery(historical, "tx-1", AuthorizationRecoveryCommitCrashPoint.AfterPrepareBeforeApply));

        Assert.Equal(before, store.Current.State);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome,
            store.Reconcile());
        Assert.Null(store.Current.PendingTransaction);
        Assert.Equal(before, store.Current.State);
    }

    [Fact]
    public void Crash_after_apply_before_acknowledgement_reconciles_as_committed_without_replay()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareHistoricalRecovery(historical, "tx-2", out _));

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Committed,
            store.ExecutePreparedRecovery(historical, "tx-2", AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement));

        Assert.Equal(2, store.Current.State.Revision);
        Assert.NotNull(store.Current.PendingTransaction);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.RecoveredCommittedOutcome,
            store.Reconcile());
        Assert.Equal(2, store.Current.State.Revision);
        Assert.Equal("tx-2", store.Current.LastCommittedTransactionId);
        Assert.Null(store.Current.PendingTransaction);
    }

    [Fact]
    public void Reconciliation_does_not_guess_when_durable_state_conflicts_with_journal()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareHistoricalRecovery(historical, "tx-3", out _));

        // A prepared transaction must not be bypassed by another writer.
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.ConflictDetected,
            store.RejectIfUnreconciled());

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome,
            store.Reconcile());
    }

    [Fact]
    public void Committed_transaction_is_recovered_exactly_once()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareHistoricalRecovery(historical, "tx-4", out _));

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Committed,
            store.ExecutePreparedRecovery(historical, "tx-4", AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement));

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.RecoveredCommittedOutcome,
            store.Reconcile());

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.NoPendingTransaction,
            store.Reconcile());
        Assert.Equal(2, store.Current.State.Revision);
    }

    [Fact]
    public void Tampered_publication_cannot_create_recoverable_transaction()
    {
        var store = Create();
        var valid = Publication("key-v1", KeyV1, 42);
        var tampered = valid with { HeadDigest = "forged" };

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.RejectedIntegrity,
            store.TryPrepareHistoricalRecovery(tampered, "tx-5", out var transaction));
        Assert.Null(transaction);
        Assert.Null(store.Current.PendingTransaction);
    }

    [Fact]
    public void Thirty_two_instances_cannot_bypass_an_unresolved_commit_fence()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareHistoricalRecovery(historical, "tx-root", out _));

        var results = new System.Collections.Concurrent.ConcurrentBag<AuthorizationRecoveryCommitReconciliationResult>();
        Parallel.For(0, 32, _ => results.Add(store.RejectIfUnreconciled()));

        Assert.Equal(32, results.Count(x => x == AuthorizationRecoveryCommitReconciliationResult.ConflictDetected));

        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.AbortedPreparedOutcome,
            store.Reconcile());
    }
}

public sealed partial class AuthorizationRecoveryControlPlaneCommitReconciliationTests
{
    [Fact]
    public void Retirement_crash_after_apply_is_reconciled_without_second_revision_advance()
    {
        var store = Create();
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPrepareRetirement("key-v1", "key-v2", 43, "tx-retire", out _));
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Committed,
            store.ExecutePreparedRetirement("key-v1", "tx-retire", AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement));
        Assert.Equal(AuthorizationRecoveryKeyStatus.Retired, store.Current.State.KeyRing.Keys["key-v1"].Status);
        Assert.Equal(2, store.Current.State.Revision);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.RecoveredCommittedOutcome,
            store.Reconcile());
        Assert.Equal(2, store.Current.State.Revision);
    }

    [Fact]
    public void Publication_crash_after_apply_is_reconciled_without_republication()
    {
        var store = Create();
        var publication = Publication("key-v2", KeyV2, 44);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Prepared,
            store.TryPreparePublication("key-v2", publication, "tx-publish", out _));
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.Committed,
            store.ExecutePreparedPublication(publication, "tx-publish", AuthorizationRecoveryCommitCrashPoint.AfterApplyBeforeCommitAcknowledgement));
        Assert.Equal(44, store.Current.State.Publication.Sequence);
        Assert.Equal(2, store.Current.State.Revision);
        Assert.Equal(
            AuthorizationRecoveryCommitReconciliationResult.RecoveredCommittedOutcome,
            store.Reconcile());
        Assert.Equal(44, store.Current.State.Publication.Sequence);
        Assert.Equal(2, store.Current.State.Revision);
    }
}
