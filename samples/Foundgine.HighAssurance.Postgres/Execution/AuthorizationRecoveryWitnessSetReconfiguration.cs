namespace Foundgine.HighAssurance.Postgres.Execution;

/// <summary>
/// Immutable snapshot of which witnesses currently participate in quorum evaluation, together with
/// the monotonic version that identifies that membership. A caller who wants to change membership
/// must present the version it believes is current, exactly like the anchor's own compare-and-advance
/// requires the caller to present the sequence/digest it believes is current.
/// </summary>
public sealed record AuthorizationRecoveryWitnessConfiguration(
    long ConfigVersion,
    IReadOnlyList<AuthorizationRecoveryQuorumWitness> Witnesses)
{
    public int Majority => (Witnesses.Count / 2) + 1;
}

public enum AuthorizationRecoveryReconfigurationOutcome
{
    Reconfigured,

    /// <summary>The proposer could not be authenticated or authorized for reconfiguration.</summary>
    UnauthorizedProposer,

    /// <summary>Fewer than a majority of the CURRENT (pre-change) witnesses were reachable.</summary>
    NoQuorum,

    /// <summary>The caller's expected configuration version is no longer current.</summary>
    StaleConfigVersion,

    /// <summary>The proposed membership itself is not a valid witness configuration.</summary>
    InvalidMembership
}

public sealed record AuthorizationRecoveryReconfigurationResult(
    bool Reconfigured,
    AuthorizationRecoveryReconfigurationOutcome Outcome,
    long ConfigVersion,
    string? Reason)
{
    public static AuthorizationRecoveryReconfigurationResult NoQuorum(long currentVersion, int reachable, int required) =>
        new(
            false,
            AuthorizationRecoveryReconfigurationOutcome.NoQuorum,
            currentVersion,
            $"Only {reachable}/{required} witnesses of the current configuration were reachable; refusing to change recovery-quorum membership.");

    public static AuthorizationRecoveryReconfigurationResult Unauthorized(long currentVersion, string reason) =>
        new(false, AuthorizationRecoveryReconfigurationOutcome.UnauthorizedProposer, currentVersion, reason);

    public static AuthorizationRecoveryReconfigurationResult Stale(long currentVersion) =>
        new(
            false,
            AuthorizationRecoveryReconfigurationOutcome.StaleConfigVersion,
            currentVersion,
            "The caller's expected witness-configuration version is no longer current.");

    public static AuthorizationRecoveryReconfigurationResult Invalid(long currentVersion, string reason) =>
        new(false, AuthorizationRecoveryReconfigurationOutcome.InvalidMembership, currentVersion, reason);

    public static AuthorizationRecoveryReconfigurationResult Committed(long newVersion) =>
        new(true, AuthorizationRecoveryReconfigurationOutcome.Reconfigured, newVersion, null);
}

/// <summary>
/// Quorum anchor (M5.38) whose witness membership can itself change safely over the anchor's
/// lifetime — replacing a decommissioned witness, adding capacity, rotating a compromised one —
/// without opening the reconfiguration path itself into a way to manufacture false authority.
///
/// Two attacks are specific to reconfiguration and are not covered by M5.38 alone:
///
/// 1. **Minority-driven reconfiguration.** If reconfiguration only required the CALLER's say-so, a
///    process operating from an isolated minority partition — or an attacker who compromised a
///    minority of witnesses — could unilaterally replace the witness set with one it fully
///    controls, then use that captured majority to mint recovery authority at will. M5.39 requires
///    a reachable majority of the CURRENT configuration before any membership change is accepted:
///    the same "no quorum, no new authority" rule from M5.38, now applied to the authority over who
///    gets to vote, not just to the authority over recovery state itself.
///
/// 2. **Stale-configuration resurrection.** Once configuration N+1 is committed, evaluating quorum
///    against configuration N must never succeed again, even if a majority of configuration N's
///    witnesses happen to still be reachable and still agree with each other. Every quorum
///    evaluation always reads the single current configuration under the same lock used to swap it,
///    so old witness handles held by a caller become inert the instant a newer configuration is
///    committed — the reconfiguration analogue of the anchor's own rollback resistance (M5.36).
///
/// Reconfiguration is itself monotonic and CAS-gated on <see cref="AuthorizationRecoveryWitnessConfiguration.ConfigVersion"/>,
/// so two concurrent reconfiguration attempts can never both apply: exactly one wins, and the loser
/// observes <see cref="AuthorizationRecoveryReconfigurationOutcome.StaleConfigVersion"/>.
///
/// Every accepted reconfiguration — including the genesis membership passed to the constructor — is
/// also appended to <see cref="Ledger"/>, a tamper-evident hash-chained record of reconfiguration
/// history (M5.40). This lets a caller who only ever observes <see cref="CurrentConfiguration"/>, as
/// this class correctly restricts them to, still independently verify the full sequence of past
/// memberships and detect alteration, truncation, or reordering of that history.
///
/// What this class deliberately does not attempt: it cannot make a reconfiguration safe if an
/// attacker already controls a genuine majority of the current, legitimate witness set — that is a
/// compromise of the layer below this one, not a defect this layer can repair. In production,
/// membership changes should additionally be authenticated and audited by the same control plane
/// that operates the witnesses themselves (per M5.38); this class adds the client-side ordering and
/// quorum-authorized-change discipline in front of that.
/// </summary>
public sealed class ReconfigurableAuthorizationRecoveryQuorumAnchor : IAuthorizationRecoveryQuorumAnchor
{
    private readonly IAuthorizationRecoveryForkAnchor _primary;
    private readonly IAuthorizationRecoveryReconfigurationProposerAuthorizer _proposerAuthorizer;
    private readonly object _configGate = new();
    private readonly SemaphoreSlim _reconfigurationGate = new(1, 1);
    private AuthorizationRecoveryReconfigurationLedger _ledger = new();
    private AuthorizationRecoveryWitnessConfiguration _config;
    private IAuthorizationRecoveryReconfigurationLedgerStore? _ledgerStore;

    public ReconfigurableAuthorizationRecoveryQuorumAnchor(
        IAuthorizationRecoveryForkAnchor primary,
        IReadOnlyList<AuthorizationRecoveryQuorumWitness> initialWitnesses,
        long initialConfigVersion = 0,
        IAuthorizationRecoveryReconfigurationProposerAuthorizer? proposerAuthorizer = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _proposerAuthorizer = proposerAuthorizer ?? DenyAllAuthorizationRecoveryReconfigurationProposerAuthorizer.Instance;
        ValidateMembership(initialWitnesses, nameof(initialWitnesses));
        _config = new AuthorizationRecoveryWitnessConfiguration(initialConfigVersion, initialWitnesses);

        // The genesis membership is itself an audit-ledger entry (M5.40): a caller who only ever
        // observes the *current* configuration can still verify the complete history back to the
        // very first membership this anchor was constructed with.
        _ledger.Append(initialConfigVersion, initialWitnesses, proposerId: null);
    }

    /// <summary>The configuration currently used to evaluate quorum. Read fresh on every call.</summary>
    public AuthorizationRecoveryWitnessConfiguration CurrentConfiguration
    {
        get { lock (_configGate) return _config; }
    }

    /// <summary>
    /// Tamper-evident, hash-chained record of every reconfiguration this anchor has ever accepted,
    /// including its genesis membership (M5.40). Independently verifiable via
    /// <see cref="AuthorizationRecoveryReconfigurationLedger.VerifyChain()"/> without trusting
    /// <see cref="CurrentConfiguration"/> at all.
    /// </summary>
    public AuthorizationRecoveryReconfigurationLedger Ledger => _ledger;

    public async ValueTask<AuthorizationRecoveryQuorumAdvanceResult> TryAdvanceAsync(
        long expectedSequence,
        string expectedDigest,
        long nextSequence,
        string nextDigest,
        string writerId,
        CancellationToken cancellationToken = default)
    {
        var config = CurrentConfiguration;
        var fallback = new AuthorizationRecoveryAnchorState(expectedSequence, expectedDigest, null);
        var corroboration = await CorroborateAsync(config, expectedSequence, expectedDigest, cancellationToken);
        if (corroboration.Availability != AuthorizationRecoveryQuorumAvailability.Available)
            return AuthorizationRecoveryQuorumAdvanceResult.NoQuorum(fallback, corroboration.Reachable, config.Majority);
        if (!corroboration.Agrees)
            return AuthorizationRecoveryQuorumAdvanceResult.Rejected(corroboration.ObservedState ?? fallback, corroboration.Reason!);

        var accepted = await _primary.TryAdvanceAsync(expectedSequence, expectedDigest, nextSequence, nextDigest, writerId, cancellationToken);
        if (!accepted)
        {
            var current = await _primary.ReadAsync(cancellationToken);
            return AuthorizationRecoveryQuorumAdvanceResult.Rejected(current, "A competing writer advanced the primary anchor first.");
        }

        return AuthorizationRecoveryQuorumAdvanceResult.Committed(new AuthorizationRecoveryAnchorState(nextSequence, nextDigest, writerId));
    }

    public async ValueTask<AuthorizationRecoveryQuorumVerifyResult> TryVerifyCommittedAsync(
        long sequence,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var config = CurrentConfiguration;
        var corroboration = await CorroborateAsync(config, sequence, digest, cancellationToken);
        if (corroboration.Availability != AuthorizationRecoveryQuorumAvailability.Available)
            return AuthorizationRecoveryQuorumVerifyResult.NoQuorum(corroboration.Reachable, config.Majority);

        return corroboration.Agrees
            ? AuthorizationRecoveryQuorumVerifyResult.Confirmed()
            : AuthorizationRecoveryQuorumVerifyResult.Mismatch(corroboration.Reason!);
    }

    /// <summary>
    /// Attempts to replace the witness membership. Requires <paramref>
    ///     <name>expectedConfigVersion</name>
    /// </paramref>
    /// to still be current and requires a reachable majority of the CURRENT (pre-change) witnesses
    /// before the swap — a minority can neither push through a reconfiguration nor race one under
    /// partition. Exactly one concurrent attempt can win; the rest observe
    /// <see cref="AuthorizationRecoveryReconfigurationOutcome.StaleConfigVersion"/>.
    /// </summary>
    /// <summary>
    /// Loads and verifies durable reconfiguration history. Startup fails closed when the chain,
    /// membership manifest, or live configuration cannot be reconciled exactly.
    /// An empty durable store is initialized with the constructor's genesis membership.
    /// </summary>
    public async ValueTask BootstrapAsync(
        IAuthorizationRecoveryReconfigurationLedgerStore store,
        IAuthorizationRecoveryWitnessResolver witnessResolver,
        CancellationToken cancellationToken = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (witnessResolver is null) throw new ArgumentNullException(nameof(witnessResolver));

        await _reconfigurationGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await store.LoadAsync(cancellationToken);
            if (snapshot.Records.Count == 0)
            {
                var current = CurrentConfiguration;
                var genesis = _ledger.Records[0];
                await store.AppendAsync(genesis,
                    current.Witnesses.Select(static w => w.WitnessId).ToArray(), cancellationToken);
                snapshot = await store.LoadAsync(cancellationToken);
            }

            VerifyDurableSnapshot(snapshot);

            var head = snapshot.Records[^1];
            if (!snapshot.MembershipByVersion.TryGetValue(head.ConfigVersion, out var ids))
                throw new AuthorizationRecoveryReconciliationException("Durable ledger head has no membership manifest.");

            var resolved = witnessResolver.Resolve(ids);
            ValidateMembership(resolved, nameof(witnessResolver));

            if (!string.Equals(
                    AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(resolved),
                    head.MembershipDigest,
                    StringComparison.OrdinalIgnoreCase))
                throw new AuthorizationRecoveryReconciliationException("Resolved witness membership does not match durable ledger head.");

            lock (_configGate)
            {
                _ledger = AuthorizationRecoveryReconfigurationLedger.Restore(snapshot.Records);
                _config = new AuthorizationRecoveryWitnessConfiguration(head.ConfigVersion, resolved);
                _ledgerStore = store;
            }
        }
        finally
        {
            _reconfigurationGate.Release();
        }
    }

    /// <summary>Restores only when the supplied live membership is exactly the durable ledger head.</summary>
    public async ValueTask RestoreAsync(
        IAuthorizationRecoveryReconfigurationLedgerStore store,
        IReadOnlyList<AuthorizationRecoveryQuorumWitness> currentWitnesses,
        long currentConfigVersion,
        CancellationToken cancellationToken = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        ValidateMembership(currentWitnesses, nameof(currentWitnesses));

        await _reconfigurationGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await store.LoadAsync(cancellationToken);
            VerifyDurableSnapshot(snapshot);
            var head = snapshot.Records[^1];
            var digest = AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(currentWitnesses);

            if (head.ConfigVersion != currentConfigVersion ||
                !string.Equals(head.MembershipDigest, digest, StringComparison.OrdinalIgnoreCase))
                throw new AuthorizationRecoveryReconciliationException(
                    "Durable ledger head does not match the supplied live configuration; refusing to serve.");

            if (!snapshot.MembershipByVersion.TryGetValue(head.ConfigVersion, out var ids) ||
                !ids.SequenceEqual(currentWitnesses.Select(static w => w.WitnessId).OrderBy(static x => x, StringComparer.Ordinal)))
                throw new AuthorizationRecoveryReconciliationException(
                    "Durable membership manifest does not match the supplied live configuration.");

            lock (_configGate)
            {
                _ledger = AuthorizationRecoveryReconfigurationLedger.Restore(snapshot.Records);
                _config = new AuthorizationRecoveryWitnessConfiguration(currentConfigVersion, currentWitnesses);
                _ledgerStore = store;
            }
        }
        finally
        {
            _reconfigurationGate.Release();
        }
    }

    private static void VerifyDurableSnapshot(AuthorizationRecoveryReconfigurationLedgerSnapshot snapshot)
    {
        var verification = AuthorizationRecoveryReconfigurationLedger.VerifyChain(snapshot.Records);
        if (!verification.Verified)
            throw new AuthorizationRecoveryReconciliationException(
                $"Durable reconfiguration ledger failed verification: {verification.Reason}");

        if (snapshot.Records.Count == 0)
            throw new AuthorizationRecoveryReconciliationException("Durable reconfiguration ledger is empty.");

        foreach (var record in snapshot.Records)
        {
            if (!snapshot.MembershipByVersion.TryGetValue(record.ConfigVersion, out var ids))
                throw new AuthorizationRecoveryReconciliationException(
                    $"Configuration version {record.ConfigVersion} has no durable membership manifest.");

            if (ids.Count == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new AuthorizationRecoveryReconciliationException(
                    $"Configuration version {record.ConfigVersion} has an invalid membership manifest.");

            var digest = AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(ids);
            if (!string.Equals(digest, record.MembershipDigest, StringComparison.OrdinalIgnoreCase))
                throw new AuthorizationRecoveryReconciliationException(
                    $"Configuration version {record.ConfigVersion} membership manifest does not match its ledger digest.");
        }
    }

    public async ValueTask<AuthorizationRecoveryReconfigurationResult> TryReconfigureAsync(
        long expectedConfigVersion,
        IReadOnlyList<AuthorizationRecoveryQuorumWitness> newWitnesses,
        AuthorizationRecoveryReconfigurationProposerCredential? proposerCredential = null,
        CancellationToken cancellationToken = default)
    {
        var invalidReason = TryValidateMembership(newWitnesses);
        if (invalidReason is not null)
        {
            var currentVersion = CurrentConfiguration.ConfigVersion;
            return AuthorizationRecoveryReconfigurationResult.Invalid(currentVersion, invalidReason);
        }

        var proposedMembershipDigest = AuthorizationRecoveryReconfigurationLedger.ComputeMembershipDigest(newWitnesses);
        await _reconfigurationGate.WaitAsync(cancellationToken);
        IAuthorizationRecoveryReconfigurationProposerCredentialLease? credentialLease = null;
        try
        {
            var current = CurrentConfiguration;
            if (expectedConfigVersion != current.ConfigVersion)
                return AuthorizationRecoveryReconfigurationResult.Stale(current.ConfigVersion);

            if (proposerCredential is null || !_proposerAuthorizer.Authorize(
                    proposerCredential, current.ConfigVersion, proposedMembershipDigest))
            {
                return AuthorizationRecoveryReconfigurationResult.Unauthorized(
                    current.ConfigVersion,
                    "Reconfiguration proposer authentication/authorization failed; refusing to change witness membership.");
            }

            // M5.44: if the authenticator exposes credential lifecycle, acquire a lease before any
            // durable write. Rotation/retirement must wait until this transition commits or fails.
            // This closes the authentication-to-commit TOCTOU window.
            if (_proposerAuthorizer is IAuthorizationRecoveryReconfigurationProposerCredentialLifecycle lifecycle)
            {
                credentialLease = await lifecycle.TryAcquireAsync(proposerCredential, cancellationToken);
                if (credentialLease is null)
                    return AuthorizationRecoveryReconfigurationResult.Unauthorized(
                        current.ConfigVersion,
                        "The proposer credential is no longer active at the lifecycle boundary; refusing to change witness membership.");
            }

            var reachable = current.Witnesses.Count(static w => w.IsReachable);
            if (reachable < current.Majority)
                return AuthorizationRecoveryReconfigurationResult.NoQuorum(
                    current.ConfigVersion, reachable, current.Majority);

            // M5.46: a credential lease is local to this process, so re-check the shared
            // authoritative lifecycle store immediately before durable publication. A different
            // instance may have revoked the credential while this instance was executing.
            if (credentialLease is not null && !await credentialLease.ValidateStillCurrentAsync(cancellationToken))
                return AuthorizationRecoveryReconfigurationResult.Unauthorized(
                    current.ConfigVersion,
                    "The proposer credential was revoked or rotated by another instance before durable commit; refusing to publish the reconfiguration.");

            var nextVersion = current.ConfigVersion + 1;
            var previousRecords = _ledger.Records;
            var candidateLedger = AuthorizationRecoveryReconfigurationLedger.Restore(previousRecords);
            var candidate = candidateLedger.Append(nextVersion, newWitnesses, proposerCredential.ProposerId);

            if (_ledgerStore is not null)
            {
                await _ledgerStore.AppendAsync(
                    candidate,
                    newWitnesses.Select(static w => w.WitnessId).ToArray(),
                    cancellationToken);
            }

            lock (_configGate)
            {
                if (_config.ConfigVersion != expectedConfigVersion)
                    throw new AuthorizationRecoveryReconciliationException(
                        "Configuration changed while the durable ledger append was being committed.");

                _ledger = candidateLedger;
                _config = new AuthorizationRecoveryWitnessConfiguration(nextVersion, newWitnesses);
            }

            return AuthorizationRecoveryReconfigurationResult.Committed(nextVersion);
        }
        finally
        {
            if (credentialLease is not null)
                await credentialLease.DisposeAsync();
            _reconfigurationGate.Release();
        }
    }

    private static async ValueTask<CorroborationResult> CorroborateAsync(
        AuthorizationRecoveryWitnessConfiguration config,
        long sequence,
        string digest,
        CancellationToken cancellationToken)
    {
        var reachable = config.Witnesses.Where(static w => w.IsReachable).ToList();
        if (reachable.Count < config.Majority)
            return new CorroborationResult(AuthorizationRecoveryQuorumAvailability.NoQuorum, reachable.Count, false, null, null);

        var observed = await Task.WhenAll(reachable.Select(w => w.Anchor.ReadAsync(cancellationToken).AsTask()));
        var distinct = observed.Distinct().ToList();
        if (distinct.Count > 1)
            return new CorroborationResult(
                AuthorizationRecoveryQuorumAvailability.Available,
                reachable.Count,
                false,
                observed[0],
                "Reachable witnesses currently disagree on recovery state; refusing to trust either branch.");

        var agreedState = distinct[0];
        var matches = agreedState.Sequence == sequence && string.Equals(agreedState.Digest, digest, StringComparison.OrdinalIgnoreCase);
        return new CorroborationResult(
            AuthorizationRecoveryQuorumAvailability.Available,
            reachable.Count,
            matches,
            agreedState,
            matches ? null : "Reachable witnesses no longer agree with the caller's candidate recovery state.");
    }

    private static void ValidateMembership(IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses, string parameterName)
    {
        var reason = TryValidateMembership(witnesses);
        if (reason is not null)
            throw new ArgumentException(reason, parameterName);
    }

    private static string? TryValidateMembership(IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses)
    {
        if (witnesses is null || witnesses.Count == 0)
            return "At least one witness is required.";
        if (witnesses.Select(static w => w.WitnessId).Distinct(StringComparer.Ordinal).Count() != witnesses.Count)
            return "Witness ids must be unique.";
        return null;
    }

    private sealed record CorroborationResult(
        AuthorizationRecoveryQuorumAvailability Availability,
        int Reachable,
        bool Agrees,
        AuthorizationRecoveryAnchorState? ObservedState,
        string? Reason);
}
