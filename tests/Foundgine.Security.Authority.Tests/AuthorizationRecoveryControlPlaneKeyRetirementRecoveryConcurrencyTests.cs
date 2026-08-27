using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlaneKeyRetirementRecoveryConcurrencyTests
{
    private static readonly byte[] KeyV1 = new byte[32];
    private static readonly byte[] KeyV2 = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();

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

    private static AuthorizationRecoveryControlPlaneKeyRetirementRecoveryConcurrency Create(
        long window = 0,
        long sequence = 43)
    {
        var keys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>
        {
            ["key-v1"] = new("key-v1", AuthorizationRecoveryKeyStatus.VerificationOnly, 1),
            ["key-v2"] = new("key-v2", AuthorizationRecoveryKeyStatus.Active, 2)
        };

        return new(
            new AuthorizationRecoveryKeyRing("key-v2", keys),
            Publication("key-v2", KeyV2, sequence),
            window,
            keyId => keyId switch
            {
                "key-v1" => KeyV1,
                "key-v2" => KeyV2,
                _ => null
            });
    }

    [Fact]
    public void Recovery_succeeds_when_it_wins_before_retirement()
    {
        var model = Create(window: 0);
        var historical = Publication("key-v1", KeyV1, 42);
        model.RecordPublication(Publication("key-v2", KeyV2, 43));

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.Recovered,
            model.TryRecoverHistorical(historical));
    }

    [Fact]
    public void Retirement_wins_then_historical_recovery_fails_closed()
    {
        var model = Create(window: 0);
        model.RecordPublication(Publication("key-v2", KeyV2, 43));

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.Retired,
            model.TryRetire("key-v1", "key-v2", 43));

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.RejectedRetiredKey,
            model.TryRecoverHistorical(Publication("key-v1", KeyV1, 42)));
    }

    [Fact]
    public void Historical_recovery_is_blocked_while_window_is_open()
    {
        var model = Create(window: 2, sequence: 43);
        var historical = Publication("key-v1", KeyV1, 42);

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.Recovered,
            model.TryRecoverHistorical(historical));

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.HistoricalPublicationStillProtected,
            model.TryRetire("key-v1", "key-v2", 43));
    }

    [Fact]
    public void Stale_retirement_cannot_follow_a_new_publication()
    {
        var model = Create(window: 0, sequence: 43);
        model.RecordPublication(Publication("key-v2", KeyV2, 44));

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.StaleRetirement,
            model.TryRetire("key-v1", "key-v2", 43));
    }

    [Fact]
    public void Concurrent_retirement_and_recovery_have_one_linearized_outcome()
    {
        var model = Create(window: 0, sequence: 43);
        var historical = Publication("key-v1", KeyV1, 42);
        var results = new ConcurrentBag<AuthorizationRecoveryRetirementRecoveryResult>();

        Parallel.For(0, 32, i =>
        {
            if ((i & 1) == 0)
            {
                results.Add(model.TryRecoverHistorical(historical));
            }
            else
            {
                results.Add(model.TryRetire("key-v1", "key-v2", 43));
            }
        });

        var retired = results.Count(x => x == AuthorizationRecoveryRetirementRecoveryResult.Retired);
        var recovered = results.Count(x => x == AuthorizationRecoveryRetirementRecoveryResult.Recovered);
        var rejected = results.Count(x => x == AuthorizationRecoveryRetirementRecoveryResult.RejectedRetiredKey);

        Assert.Equal(1, retired);
        Assert.True(recovered + rejected > 0);
        Assert.Equal(AuthorizationRecoveryKeyStatus.Retired,
            model.CurrentRing.Keys["key-v1"].Status);
    }

    [Fact]
    public void Atomic_recovery_and_promotion_rejects_tampered_authoritative_publication()
    {
        var model = Create();
        var current = model.CurrentPublication;
        var tampered = current with { HeadDigest = "forged" };

        Assert.Equal(
            AuthorizationRecoveryRetirementRecoveryResult.PromotionRejected,
            model.TryRecoverAndPromote(tampered));
    }
}
