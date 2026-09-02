using System.Collections.Concurrent;
using Foundgine.Runtime.ControlPlane;
using Xunit;

public sealed class AuthorizationRecoveryControlPlaneCrossInstanceConcurrencyTests
{
    private static readonly byte[] KeyV1 = new byte[32];
    private static readonly byte[] KeyV2 = Enumerable.Range(0, 32)
        .Select(i => (byte)(i + 1)).ToArray();

    private static AuthorizationRecoveryControlPlanePublication Publication(
        string keyId, byte[] key, long sequence) =>
        new(
            9,
            "primary",
            sequence,
            $"digest-{sequence}",
            keyId,
            AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
            AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
                9, "primary", sequence, $"digest-{sequence}", keyId, key));

    private static AuthorizationRecoveryControlPlaneCrossInstanceConcurrency Create(
        long window = 0,
        long sequence = 43)
    {
        var ring = new AuthorizationRecoveryKeyRing(
            "key-v2",
            new Dictionary<string, AuthorizationRecoveryIntegrityKey>
            {
                ["key-v1"] = new("key-v1", AuthorizationRecoveryKeyStatus.VerificationOnly, 1),
                ["key-v2"] = new("key-v2", AuthorizationRecoveryKeyStatus.Active, 2)
            });

        var publication = Publication("key-v2", KeyV2, sequence);

        return new(
            new AuthorizationRecoveryCrossInstanceState(1, ring, publication),
            window,
            keyId => keyId switch
            {
                "key-v1" => KeyV1,
                "key-v2" => KeyV2,
                _ => null
            });
    }

    [Fact]
    public void Instance_A_prepares_recovery_then_instance_B_retires_key_and_A_fails_closed()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared,
            store.TryPrepareHistoricalRecovery(historical, out var decision));

        var before = store.Current;

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.Retired,
            store.TryRetire("key-v1", "key-v2", 43, before.Revision));

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedStaleState,
            store.TryCommitHistoricalRecovery(decision!));
    }

    [Fact]
    public void Instance_B_publication_transition_invalidates_A_recovery_decision()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared,
            store.TryPrepareHistoricalRecovery(historical, out var decision));

        var before = store.Current;
        var newer = Publication("key-v2", KeyV2, 44);

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.PublicationCommitted,
            store.TryPublish("key-v2", before.Revision, newer));

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedStaleState,
            store.TryCommitHistoricalRecovery(decision!));
    }

    [Fact]
    public void Recovery_wins_when_commit_precedes_cross_instance_retirement()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared,
            store.TryPrepareHistoricalRecovery(historical, out var decision));

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.Recovered,
            store.TryCommitHistoricalRecovery(decision!));

        var current = store.Current;

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.Retired,
            store.TryRetire("key-v1", "key-v2", 43, current.Revision));

        Assert.Equal(
            AuthorizationRecoveryKeyStatus.Retired,
            store.Current.KeyRing.Keys["key-v1"].Status);
    }

    [Fact]
    public void Concurrent_32_instance_attack_has_no_stale_recovery_after_retirement_commit()
    {
        var store = Create();
        var historical = Publication("key-v1", KeyV1, 42);
        var decisions = new ConcurrentBag<AuthorizationRecoveryCrossInstanceDecision>();
        var prepareResults = new ConcurrentBag<AuthorizationRecoveryCrossInstanceResult>();
        var barrier = new Barrier(32);

        Parallel.For(0, 32, _ =>
        {
            var result = store.TryPrepareHistoricalRecovery(
                historical, out var decision);

            prepareResults.Add(result);
            if (decision is not null)
                decisions.Add(decision);

            barrier.SignalAndWait();
        });

        Assert.Equal(
            32,
            prepareResults.Count(x =>
                x == AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared));

        var observed = store.Current;

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.Retired,
            store.TryRetire(
                "key-v1",
                "key-v2",
                observed.Publication.Sequence,
                observed.Revision));

        var commitResults = new ConcurrentBag<AuthorizationRecoveryCrossInstanceResult>();

        Parallel.ForEach(decisions, decision =>
        {
            commitResults.Add(store.TryCommitHistoricalRecovery(decision));
        });

        Assert.Equal(
            32,
            commitResults.Count(x =>
                x == AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedStaleState));

        Assert.DoesNotContain(
            commitResults,
            x => x == AuthorizationRecoveryCrossInstanceResult.Recovered);

        Assert.Equal(
            AuthorizationRecoveryKeyStatus.Retired,
            store.Current.KeyRing.Keys["key-v1"].Status);
    }

    [Fact]
    public void Stale_retirement_from_instance_A_cannot_commit_after_instance_B_publication()
    {
        var store = Create();
        var observed = store.Current;

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.PublicationCommitted,
            store.TryPublish(
                "key-v2",
                observed.Revision,
                Publication("key-v2", KeyV2, 44)));

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.StaleRetirement,
            store.TryRetire(
                "key-v1",
                "key-v2",
                43,
                observed.Revision));

        Assert.Equal(
            AuthorizationRecoveryKeyStatus.VerificationOnly,
            store.Current.KeyRing.Keys["key-v1"].Status);
    }

    [Fact]
    public void Tampered_historical_publication_cannot_be_prepared_by_any_instance()
    {
        var store = Create();
        var valid = Publication("key-v1", KeyV1, 42);
        var tampered = valid with { HeadDigest = "forged" };

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryRejectedIntegrity,
            store.TryPrepareHistoricalRecovery(tampered, out var decision));

        Assert.Null(decision);
    }

    [Fact]
    public void Historical_recovery_is_allowed_until_window_closes()
    {
        var store = Create(window: 2, sequence: 43);
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.RecoveryPrepared,
            store.TryPrepareHistoricalRecovery(historical, out var decision));

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.Recovered,
            store.TryCommitHistoricalRecovery(decision!));

        var current = store.Current;

        Assert.Equal(
            AuthorizationRecoveryCrossInstanceResult.HistoricalPublicationStillProtected,
            store.TryRetire("key-v1", "key-v2", 43, current.Revision));
    }
}
