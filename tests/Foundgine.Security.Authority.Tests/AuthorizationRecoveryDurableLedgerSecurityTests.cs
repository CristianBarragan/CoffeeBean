using Foundgine.Runtime.ControlPlane;
using Foundgine.HighAssurance.Postgres.Execution;
using Xunit;

namespace Foundgine.Runtime.ControlPlane.Tests;

public sealed class AuthorizationRecoveryDurableLedgerSecurityTests
{
    private const string Genesis = AuthorizationRecoveryAnchorState.GenesisDigest;

    private static (IAuthorizationRecoveryForkAnchor Primary, IReadOnlyList<AuthorizationRecoveryQuorumWitness> Witnesses)
        Cluster(params string[] ids)
    {
        var primary = new InMemoryAuthorizationRecoveryForkAnchor();
        return (primary, ids.Select(id => new AuthorizationRecoveryQuorumWitness(id, primary, () => true)).ToArray());
    }

    private static AuthorizationRecoveryReconfigurationProposerCredential Proposer(
        long version, IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses, string id = "operator-1", string fingerprint = "fp-1") =>
        new(id, fingerprint, version, AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(witnesses));

    [Fact]
    public async Task Bootstrap_seeds_empty_durable_store_and_persists_genesis()
    {
        var (primary, witnesses) = Cluster("w1", "w2", "w3");
        var store = new InMemoryAuthorizationRecoveryReconfigurationLedgerStore();
        var anchor = new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" }));

        await anchor.BootstrapAsync(store, new Resolver(witnesses));

        var snapshot = await store.LoadAsync();
        Assert.Single(snapshot.Records);
        Assert.Equal(0, snapshot.Records[0].ConfigVersion);
        Assert.True(anchor.Ledger.VerifyChain().Verified);
    }

    [Fact]
    public async Task Bootstrap_refuses_tampered_durable_ledger()
    {
        var (primary, witnesses) = Cluster("w1", "w2", "w3");
        var store = new InMemoryAuthorizationRecoveryReconfigurationLedgerStore();
        var anchor = new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" }));
        await anchor.BootstrapAsync(store, new Resolver(witnesses));

        var snapshot = await store.LoadAsync();
        var tampered = snapshot.Records.ToArray();
        tampered[0] = tampered[0] with { MembershipDigest = "deadbeef" };

        var bad = new TamperStore(new AuthorizationRecoveryReconfigurationLedgerSnapshot(
            tampered, snapshot.MembershipByVersion));
        await Assert.ThrowsAsync<AuthorizationRecoveryReconciliationException>(
            () => anchor.BootstrapAsync(bad, new Resolver(witnesses)).AsTask());
    }

    [Fact]
    public async Task Bootstrap_refuses_missing_membership_manifest()
    {
        var (primary, witnesses) = Cluster("w1", "w2", "w3");
        var store = new InMemoryAuthorizationRecoveryReconfigurationLedgerStore();
        var anchor = new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" }));
        await anchor.BootstrapAsync(store, new Resolver(witnesses));

        var snapshot = await store.LoadAsync();
        var missing = new AuthorizationRecoveryReconfigurationLedgerSnapshot(
            snapshot.Records, new Dictionary<long, IReadOnlyList<string>>());
        await Assert.ThrowsAsync<AuthorizationRecoveryReconciliationException>(
            () => anchor.RestoreAsync(new TamperStore(missing), witnesses, 0).AsTask());
    }

    [Fact]
    public async Task Reconfiguration_is_durable_before_live_membership_changes()
    {
        var (primary, witnesses) = Cluster("w1", "w2", "w3");
        var store = new InMemoryAuthorizationRecoveryReconfigurationLedgerStore();
        var anchor = new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" }));
        await anchor.BootstrapAsync(store, new Resolver(witnesses));

        var next = Cluster("w1", "w2", "w4").Witnesses;
        var result = await anchor.TryReconfigureAsync(0, next, Proposer(0, next));
        Assert.True(result.Reconfigured);

        var snapshot = await store.LoadAsync();
        Assert.Equal(1, snapshot.Records[^1].ConfigVersion);
        Assert.Equal(1, anchor.CurrentConfiguration.ConfigVersion);
        Assert.Equal(
            AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(next),
            snapshot.Records[^1].MembershipDigest);
    }

    [Fact]
    public async Task Restore_refuses_live_configuration_that_does_not_match_ledger_head()
    {
        var (primary, witnesses) = Cluster("w1", "w2", "w3");
        var store = new InMemoryAuthorizationRecoveryReconfigurationLedgerStore();
        var anchor = new ReconfigurableAuthorizationRecoveryQuorumAnchor(primary, witnesses, 0, new FingerprintAuthorizationRecoveryReconfigurationProposerAuthorizer(new Dictionary<string, string> { ["operator-1"] = "fp-1", ["control-plane-1"] = "fp-1", ["control-plane-2"] = "fp-1" }));
        await anchor.BootstrapAsync(store, new Resolver(witnesses));

        var wrong = Cluster("w1", "w2", "evil").Witnesses;
        await Assert.ThrowsAsync<AuthorizationRecoveryReconciliationException>(
            () => anchor.RestoreAsync(store, wrong, 0).AsTask());
    }

    private sealed class Resolver : IAuthorizationRecoveryWitnessResolver
    {
        private readonly Dictionary<string, AuthorizationRecoveryQuorumWitness> _map;
        public Resolver(IEnumerable<AuthorizationRecoveryQuorumWitness> witnesses) =>
            _map = witnesses.ToDictionary(w => w.WitnessId, StringComparer.Ordinal);

        public IReadOnlyList<AuthorizationRecoveryQuorumWitness> Resolve(IReadOnlyList<string> ids) =>
            ids.Select(id => _map.TryGetValue(id, out var witness)
                ? witness
                : throw new AuthorizationRecoveryReconciliationException($"Unknown witness '{id}'.")).ToArray();
    }

    private sealed class TamperStore : IAuthorizationRecoveryReconfigurationLedgerStore
    {
        private readonly AuthorizationRecoveryReconfigurationLedgerSnapshot _snapshot;
        public TamperStore(AuthorizationRecoveryReconfigurationLedgerSnapshot snapshot) => _snapshot = snapshot;
        public ValueTask<AuthorizationRecoveryReconfigurationLedgerSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_snapshot);
        public ValueTask AppendAsync(AuthorizationRecoveryReconfigurationAuditRecord record, IReadOnlyList<string> witnessIds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
