using System.Collections.Concurrent;
using System.Security.Cryptography;
using Foundgine.Authorization;
using Xunit;

namespace Foundgine.Tests;

public sealed class AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplicationTests
{
    private static readonly byte[] KeyV1 = SHA256.HashData("m5.71-key-v1"u8.ToArray());
    private static readonly byte[] KeyV2 = SHA256.HashData("m5.71-key-v2"u8.ToArray());

    private static (
        InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore Store,
        AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication A,
        AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication B)
        CreatePair()
    {
        var store = new InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore();
        var a = new AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication(store);
        var b = new AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycleReplication(store);
        return (store, a, b);
    }

    private static AuthorizationRecoveryRepairProposerCredential Credential(
        string credentialId = "cred-v1",
        string fingerprint = "fp-v1",
        long sequence = 1,
        string transactionId = "repair-20-21",
        byte[]? key = null)
    {
        var credential = new AuthorizationRecoveryRepairProposerCredential(
            "operator-a", credentialId, sequence, fingerprint, transactionId,
            20, "fp20", "h20", 21, "fp21", "h21", "", "v1");
        return credential with
        {
            Proof = AuthorizationRecoveryControlPlaneRepairProposerCredentialLifecycle.CreateProof(
                credential, key ?? KeyV1)
        };
    }

    [Fact]
    public async Task Registration_is_shared_across_instances()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        await using var lease = await b.TryAuthorizeAsync(Credential());
        Assert.NotNull(lease);
        Assert.Equal(1, lease!.Snapshot.CredentialSequence);
    }

    [Fact]
    public async Task Revocation_propagates_and_old_credential_fails_closed()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        await a.RevokeAsync("operator-a");

        await using var lease = await b.TryAuthorizeAsync(Credential());
        Assert.Null(lease);
        Assert.Equal(
            AuthorizationRecoveryRepairProposerCredentialState.Revoked,
            (await b.SnapshotAsync("operator-a")).State);
    }

    [Fact]
    public async Task Rotation_propagates_monotonically_and_old_sequence_is_fenced()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);
        await a.RotateAsync("operator-a", "cred-v2", "fp-v2", 1);
        await b.RegisterAsync("operator-a", "cred-v2", "fp-v2", KeyV2);

        await using var oldLease = await b.TryAuthorizeAsync(Credential());
        Assert.Null(oldLease);

        await using var currentLease = await b.TryAuthorizeAsync(
            Credential("cred-v2", "fp-v2", 2, key: KeyV2));
        Assert.NotNull(currentLease);
    }

    [Fact(Skip = "WIP")]
    public async Task Stale_replica_cannot_resurrect_revoked_generation()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);
        await b.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        await a.RevokeAsync("operator-a");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await b.RotateAsync("operator-a", "cred-v2", "fp-v2", 1));
        Assert.Equal(
            AuthorizationRecoveryRepairProposerCredentialState.Revoked,
            (await b.SnapshotAsync("operator-a")).State);
    }

    [Fact]
    public async Task Already_acquired_lease_is_invalidated_after_remote_revocation()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        await using var lease = await b.TryAuthorizeAsync(Credential());
        Assert.NotNull(lease);

        await a.RevokeAsync("operator-a");

        Assert.False(await lease!.ValidateStillCurrentAsync());
    }

    [Fact]
    public async Task Sequence_rollback_is_rejected_by_authoritative_store()
    {
        var store = new InMemoryAuthorizationRecoveryRepairProposerCredentialLifecycleStore();
        await store.CompareAndSetAsync(
            new("operator-a", "cred-v1", "fp-v1", 1,
                AuthorizationRecoveryRepairProposerCredentialState.Active), 0);

        await Assert.ThrowsAsync<AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException>(
            async () => await store.CompareAndSetAsync(
                new("operator-a", "cred-old", "fp-old", 1,
                    AuthorizationRecoveryRepairProposerCredentialState.Active), 1));
    }

    [Fact]
    public async Task Concurrent_instances_converge_without_sequence_rollback()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        var successes = 0;
        var conflicts = 0;
        var observed = await a.SnapshotAsync("operator-a");
        // A Barrier forces all 32 attempts to reach the read-then-CAS race at
        // the same instant. Without it, the store's read+compare-and-set is
        // fast enough (in-memory, lock-guarded, no real I/O) that Task.WhenAll
        // over plain async lambdas tends to run them to completion one at a
        // time rather than actually contending, which made this test flaky
        // rather than exercising the single-winner guarantee it asserts.
        var barrier = new Barrier(32);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(async () =>
        {
            var instance = (i & 1) == 0 ? a : b;
            barrier.SignalAndWait();
            try
            {
                await instance.RotateAsync("operator-a", $"cred-{i}", $"fp-{i}", observed.CredentialSequence);
                Interlocked.Increment(ref successes);
            }
            catch (AuthorizationRecoveryRepairProposerCredentialLifecycleConflictException)
            {
                Interlocked.Increment(ref conflicts);
            }
        })));

        Assert.Equal(1, successes);
        Assert.Equal(31, conflicts);

        var snapshotA = await a.SnapshotAsync("operator-a");
        var snapshotB = await b.SnapshotAsync("operator-a");
        Assert.Equal(snapshotA, snapshotB);
        Assert.Equal(2, snapshotA.CredentialSequence);
    }

    [Fact]
    public async Task Thirty_two_concurrent_authorization_attempts_fail_closed_after_remote_revocation()
    {
        var (_, a, b) = CreatePair();
        await a.RegisterAsync("operator-a", "cred-v1", "fp-v1", KeyV1);

        var authorized = new ConcurrentBag<bool>();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
        {
            var credential = Credential(transactionId: $"repair-{i}-21");
            await using var lease = await b.TryAuthorizeAsync(credential);
            authorized.Add(lease is not null);
        }));

        await a.RevokeAsync("operator-a");

        await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
        {
            var credential = Credential(transactionId: $"post-revoke-{i}-21");
            await using var lease = await b.TryAuthorizeAsync(credential);
            Assert.Null(lease);
        }));

        Assert.Equal(32, authorized.Count);
        Assert.All(authorized, Assert.True);
    }
}
