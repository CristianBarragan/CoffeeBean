using System.Collections.Concurrent;
using Foundgine.Security.Authority;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePublicationKeyRetirementTests
{
    private static AuthorizationRecoveryControlPlanePublicationKeyRetirement Create(long window = 2)
    {
        var keys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>
        {
            ["key-v1"] = new("key-v1", AuthorizationRecoveryKeyStatus.VerificationOnly, 1),
            ["key-v2"] = new("key-v2", AuthorizationRecoveryKeyStatus.Active, 2)
        };

        return new AuthorizationRecoveryControlPlanePublicationKeyRetirement(
            new AuthorizationRecoveryKeyRing("key-v2", keys), window);
    }

    private static AuthorizationRecoveryControlPlanePublication Publication(
        string keyId, long sequence) => new(
        9,
        "secondary",
        sequence,
        $"digest-{sequence}",
        keyId,
        AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
        $"tag-{sequence}");

    [Fact]
    public void Retiring_key_is_blocked_while_historical_publication_is_inside_window()
    {
        var retirement = Create(window: 2);
        retirement.RecordAuthoritativePublication(Publication("key-v1", 42));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));

        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.HistoricalPublicationStillProtected,
            retirement.TryRetire("key-v1", "key-v2", 43));

        Assert.Equal(AuthorizationRecoveryKeyStatus.VerificationOnly,
            retirement.Current.Keys["key-v1"].Status);
    }

    [Fact]
    public void Retirement_is_allowed_after_verification_window_closes()
    {
        var retirement = Create(window: 2);
        retirement.RecordAuthoritativePublication(Publication("key-v1", 42));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 45));

        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.Retired,
            retirement.TryRetire("key-v1", "key-v2", 45));

        Assert.Equal(AuthorizationRecoveryKeyStatus.Retired,
            retirement.Current.Keys["key-v1"].Status);
    }

    [Fact]
    public void Active_key_cannot_be_retired_even_when_window_is_closed()
    {
        var retirement = Create(window: 0);
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));

        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.CannotRetireActiveKey,
            retirement.TryRetire("key-v2", "key-v2", 43));
    }

    [Fact]
    public void Stale_retirement_cannot_retire_after_new_publication()
    {
        var retirement = Create(window: 0);
        retirement.RecordAuthoritativePublication(Publication("key-v1", 42));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));

        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.StaleRetirement,
            retirement.TryRetire("key-v1", "key-v2", 42));

        Assert.Equal(AuthorizationRecoveryKeyStatus.VerificationOnly,
            retirement.Current.Keys["key-v1"].Status);
    }

    [Fact]
    public void Concurrent_retirement_has_exactly_one_winner()
    {
        var retirement = Create(window: 0);
        retirement.RecordAuthoritativePublication(Publication("key-v1", 42));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));

        var results = new ConcurrentBag<AuthorizationRecoveryKeyRetirementResult>();
        Parallel.For(0, 32, _ =>
        {
            results.Add(retirement.TryRetire("key-v1", "key-v2", 43));
        });

        Assert.Equal(1, results.Count(x => x == AuthorizationRecoveryKeyRetirementResult.Retired));
        Assert.Equal(AuthorizationRecoveryKeyStatus.Retired,
            retirement.Current.Keys["key-v1"].Status);
    }

    [Fact]
    public void Retired_generation_cannot_be_returned_to_verification()
    {
        var retirement = Create(window: 0);
        retirement.RecordAuthoritativePublication(Publication("key-v1", 42));
        retirement.RecordAuthoritativePublication(Publication("key-v2", 43));
        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.Retired,
            retirement.TryRetire("key-v1", "key-v2", 43));

        Assert.Equal(
            AuthorizationRecoveryKeyRetirementResult.VerificationRejected,
            retirement.CheckVerification("key-v1"));
    }
}
