using System.Collections.Concurrent;
using System.Text;
using Foundgine.Authorization;
using Xunit;

public sealed class AuthorizationRecoveryControlPlanePublicationRotationTests
{
    private static readonly Dictionary<string, byte[]> Keys = new(StringComparer.Ordinal)
    {
        ["key-v1"] = Encoding.UTF8.GetBytes("test-integrity-key-v1"),
        ["key-v2"] = Encoding.UTF8.GetBytes("test-integrity-key-v2")
    };

    private static AuthorizationRecoveryControlPlanePublicationRotation Create()
    {
        const long epoch = 8;
        const string owner = "primary";
        const long sequence = 42;
        const string digest = "digest-A";
        const string keyId = "key-v1";

        var tag = AuthorizationRecoveryControlPlanePublicationIntegrity.ComputeTag(
            epoch, owner, sequence, digest, keyId, Keys[keyId]);

        var ring = new AuthorizationRecoveryKeyRing(
            keyId,
            new Dictionary<string, AuthorizationRecoveryIntegrityKey>
            {
                [keyId] = new(keyId, AuthorizationRecoveryKeyStatus.Active, 1)
            });

        var publication = new AuthorizationRecoveryControlPlanePublication(
            epoch,
            owner,
            sequence,
            digest,
            keyId,
            AuthorizationRecoveryControlPlanePublicationIntegrity.SupportedAlgorithm,
            tag);

        return new AuthorizationRecoveryControlPlanePublicationRotation(
            new AuthorizationRecoveryControlPlanePublicationRotationState(ring, publication),
            id => Keys.TryGetValue(id, out var key) ? key : null);
    }

    [Fact]
    public void Rotation_and_first_new_publication_are_one_atomic_state()
    {
        var coordinator = Create();

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.RotatedAndPublished,
            coordinator.TryRotateAndPublish(
                "key-v1", "key-v2", 2,
                9, "secondary", 43, "digest-B"));

        var state = coordinator.Current;

        Assert.Equal("key-v2", state.KeyRing.ActiveKeyId);
        Assert.Equal(AuthorizationRecoveryKeyStatus.VerificationOnly,
            state.KeyRing.Keys["key-v1"].Status);
        Assert.Equal(AuthorizationRecoveryKeyStatus.Active,
            state.KeyRing.Keys["key-v2"].Status);
        Assert.Equal("key-v2", state.Publication.IntegrityKeyId);
        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.VerificationAllowed,
            coordinator.VerifyCurrentPublication());
    }

    [Fact]
    public void Old_publication_remains_verifiable_across_rotation_before_retirement()
    {
        var coordinator = Create();
        var oldPublication = coordinator.Current.Publication;

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.RotatedAndPublished,
            coordinator.TryRotateAndPublish(
                "key-v1", "key-v2", 2,
                9, "secondary", 43, "digest-B"));

        // The authoritative publication is new, while the old publication can
        // still be independently verified with its verification-only key.
        Assert.True(
            AuthorizationRecoveryControlPlanePublicationIntegrity.Verify(
                oldPublication, Keys["key-v1"]));
        Assert.Equal(
            AuthorizationRecoveryKeyStatus.VerificationOnly,
            coordinator.Current.KeyRing.Keys["key-v1"].Status);
    }

    [Fact]
    public void Version_rollback_is_rejected_even_with_a_fresh_key_id()
    {
        var coordinator = Create();

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.InvalidSuccessorVersion,
            coordinator.TryRotateAndPublish(
                "key-v1", "key-v2", 1,
                9, "secondary", 43, "digest-B"));

        Assert.Equal("key-v1", coordinator.Current.KeyRing.ActiveKeyId);
        Assert.Equal("key-v1", coordinator.Current.Publication.IntegrityKeyId);
    }

    [Fact]
    public void Publication_sequence_rollback_is_rejected_before_signing()
    {
        var coordinator = Create();

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.StalePublication,
            coordinator.TryPublish(
                "key-v1", 9, "primary", 41, "digest-old"));

        Assert.Equal(42, coordinator.Current.Publication.Sequence);
        Assert.Equal("key-v1", coordinator.Current.Publication.IntegrityKeyId);
    }

    [Fact]
    public void New_publication_cannot_reference_unavailable_successor_key()
    {
        var coordinator = Create();

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.SigningKeyUnavailable,
            coordinator.TryRotateAndPublish(
                "key-v1", "key-v3", 3,
                9, "secondary", 43, "digest-B"));

        Assert.Equal("key-v1", coordinator.Current.KeyRing.ActiveKeyId);
        Assert.Equal("key-v1", coordinator.Current.Publication.IntegrityKeyId);
    }

    [Fact]
    public void Stale_writer_cannot_publish_under_old_generation_after_rotation()
    {
        var coordinator = Create();

        coordinator.TryRotateAndPublish(
            "key-v1", "key-v2", 2,
            9, "secondary", 43, "digest-B");

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.StalePublicationWrite,
            coordinator.TryPublish(
                "key-v1", 10, "secondary", 44, "digest-C"));

        Assert.Equal("key-v2", coordinator.Current.Publication.IntegrityKeyId);
        Assert.Equal(43, coordinator.Current.Publication.Sequence);
    }

    [Fact]
    public void Historical_publication_is_rejected_after_its_generation_is_retired()
    {
        var coordinator = Create();
        var oldPublication = coordinator.Current.Publication;

        coordinator.TryRotateAndPublish(
            "key-v1", "key-v2", 2,
            9, "secondary", 43, "digest-B");

        var current = coordinator.Current;
        var retiredRing = new AuthorizationRecoveryKeyRing(
            current.KeyRing.ActiveKeyId,
            new Dictionary<string, AuthorizationRecoveryIntegrityKey>
            {
                ["key-v1"] = current.KeyRing.Keys["key-v1"] with
                {
                    Status = AuthorizationRecoveryKeyStatus.Retired
                },
                ["key-v2"] = current.KeyRing.Keys["key-v2"]
            });

        var retiredCoordinator = new AuthorizationRecoveryControlPlanePublicationRotation(
            new AuthorizationRecoveryControlPlanePublicationRotationState(
                retiredRing, current.Publication),
            id => Keys.TryGetValue(id, out var key) ? key : null);

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.VerificationRejected,
            retiredCoordinator.VerifyPublication(oldPublication));

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.VerificationAllowed,
            retiredCoordinator.VerifyCurrentPublication());
    }

    [Fact]
    public void Concurrent_rotations_from_same_generation_have_exactly_one_winner()
    {
        var coordinator = Create();
        var results = new ConcurrentBag<AuthorizationRecoveryPublicationRotationResult>();

        Parallel.For(0, 32, i =>
        {
            var keyId = $"key-v{i + 2}";
            // Only key-v2 is resolvable; all other attempts must fail without
            // exposing a partially activated generation.
            if (keyId == "key-v2")
            {
                results.Add(coordinator.TryRotateAndPublish(
                    "key-v1", keyId, 2,
                    9, $"secondary-{i}", 43, "digest-B"));
            }
            else
            {
                results.Add(coordinator.TryRotateAndPublish(
                    "key-v1", keyId, i + 2,
                    9, $"secondary-{i}", 43, "digest-B"));
            }
        });

        Assert.Equal(
            1,
            results.Count(x =>
                x == AuthorizationRecoveryPublicationRotationResult.RotatedAndPublished));

        Assert.Equal("key-v2", coordinator.Current.KeyRing.ActiveKeyId);
        Assert.Equal("key-v2", coordinator.Current.Publication.IntegrityKeyId);
        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.VerificationAllowed,
            coordinator.VerifyCurrentPublication());
    }

    [Fact]
    public void Promotion_rejects_tampered_or_stale_publication()
    {
        var coordinator = Create();

        var stale = coordinator.Current.Publication with
        {
            Sequence = coordinator.Current.Publication.Sequence - 1
        };

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.PromotionRejected,
            coordinator.TryPromote(stale));

        Assert.Equal(
            AuthorizationRecoveryPublicationRotationResult.PromotionVerified,
            coordinator.TryPromote(coordinator.Current.Publication));
    }
}
