using System.Collections.Concurrent;
using Foundgine.Runtime.ControlPlane;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePublicationKeyLifecycleTests
{
    private static AuthorizationRecoveryControlPlanePublicationKeyLifecycle Create()
    {
        var keys = new Dictionary<string, AuthorizationRecoveryIntegrityKey>
        {
            ["key-v1"] = new("key-v1", AuthorizationRecoveryKeyStatus.Active, 1)
        };

        return new AuthorizationRecoveryControlPlanePublicationKeyLifecycle(
            new AuthorizationRecoveryKeyRing("key-v1", keys));
    }

    [Fact]
    public void Rotation_makes_new_key_active_and_old_key_verification_only()
    {
        var lifecycle = Create();

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.Activated,
            lifecycle.Rotate("key-v1", "key-v2", 2));

        Assert.Equal("key-v2", lifecycle.Current.ActiveKeyId);
        Assert.Equal(AuthorizationRecoveryKeyStatus.VerificationOnly, lifecycle.Current.Keys["key-v1"].Status);
        Assert.Equal(AuthorizationRecoveryKeyStatus.Active, lifecycle.Current.Keys["key-v2"].Status);
    }

    [Fact]
    public void Verification_only_key_can_verify_but_cannot_be_active_again_after_retirement()
    {
        var lifecycle = Create();

        lifecycle.Rotate("key-v1", "key-v2", 2);

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.VerificationAllowed,
            lifecycle.CheckVerification("key-v1"));

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.Retired,
            lifecycle.Retire("key-v1", "key-v2"));

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.VerificationRejected,
            lifecycle.CheckVerification("key-v1"));

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.CannotActivateRetiredKey,
            lifecycle.Rotate("key-v2", "key-v1", 1));
    }

    [Fact]
    public void Active_key_cannot_be_retired_before_rotation()
    {
        var lifecycle = Create();

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.CannotRetireActiveKey,
            lifecycle.Retire("key-v1", "key-v1"));
    }

    [Fact]
    public void Stale_rotation_cannot_roll_back_active_key()
    {
        var lifecycle = Create();

        lifecycle.Rotate("key-v1", "key-v2", 2);

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.StaleRotation,
            lifecycle.Rotate("key-v1", "key-v3", 3));

        Assert.Equal("key-v2", lifecycle.Current.ActiveKeyId);
    }

    [Fact]
    public void Concurrent_rotation_from_same_key_has_one_winner()
    {
        var lifecycle = Create();
        var results = new ConcurrentBag<AuthorizationRecoveryKeyLifecycleResult>();

        Parallel.For(0, 32, i =>
        {
            results.Add(lifecycle.Rotate("key-v1", $"key-v{i + 2}", i + 2));
        });

        Assert.Equal(1, results.Count(x => x == AuthorizationRecoveryKeyLifecycleResult.Activated));
        Assert.Equal(2, lifecycle.Current.Keys.Count);
    }

    [Fact]
    public void Retired_key_cannot_be_reactivated()
    {
        var lifecycle = Create();

        lifecycle.Rotate("key-v1", "key-v2", 2);
        lifecycle.Retire("key-v1", "key-v2");

        Assert.Equal(
            AuthorizationRecoveryKeyLifecycleResult.CannotActivateRetiredKey,
            lifecycle.Rotate("key-v2", "key-v1", 1));
    }
}
