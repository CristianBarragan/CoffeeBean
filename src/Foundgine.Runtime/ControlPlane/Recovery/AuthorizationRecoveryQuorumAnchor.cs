namespace Foundgine.Runtime.ControlPlane;

/// <summary>
/// Availability classification for a quorum-gated recovery-anchor operation. Reported separately
/// from the boolean outcome so callers can distinguish "the operation was evaluated and rejected"
/// from "the operation could not be safely evaluated at all".
/// </summary>
public enum AuthorizationRecoveryQuorumAvailability
{
 /// <summary>A majority of configured witnesses were reachable and answered consistently.</summary>
 Available,

 /// <summary>Fewer than a majority of witnesses were reachable. No new authority may be created.</summary>
 NoQuorum
}

/// <summary>
/// Result of an attempted quorum-gated advance. <see cref="Advanced"/> can only be <c>true</c> when
/// <see cref="Availability"/> is <see cref="AuthorizationRecoveryQuorumAvailability.Available"/>: a
/// <see cref="AuthorizationRecoveryQuorumAvailability.NoQuorum"/> result never creates new authority.
/// </summary>
public sealed record AuthorizationRecoveryQuorumAdvanceResult(
 bool Advanced,
 AuthorizationRecoveryQuorumAvailability Availability,
 AuthorizationRecoveryAnchorState State,
 string? Reason)
{
 public static AuthorizationRecoveryQuorumAdvanceResult NoQuorum(AuthorizationRecoveryAnchorState lastKnown, int reachable, int required) =>
 new(
 false,
 AuthorizationRecoveryQuorumAvailability.NoQuorum,
 lastKnown,
 $"Only {reachable}/{required} required witnesses were reachable; refusing to create new recovery authority.");

 public static AuthorizationRecoveryQuorumAdvanceResult Rejected(AuthorizationRecoveryAnchorState state, string reason) =>
 new(false, AuthorizationRecoveryQuorumAvailability.Available, state, reason);

 public static AuthorizationRecoveryQuorumAdvanceResult Committed(AuthorizationRecoveryAnchorState state) =>
 new(true, AuthorizationRecoveryQuorumAvailability.Available, state, null);
}

/// <summary>
/// Result of a read-only verification of an already-committed (sequence, digest) pair. Verification
/// never advances the anchor and is evaluated independently of the write path: it can confirm or
/// refute a caller's belief about already-sealed state, but it can never itself create a branch.
/// </summary>
public sealed record AuthorizationRecoveryQuorumVerifyResult(
 bool Verified,
 AuthorizationRecoveryQuorumAvailability Availability,
 string? Reason)
{
 public static AuthorizationRecoveryQuorumVerifyResult NoQuorum(int reachable, int required) =>
 new(
 false,
 AuthorizationRecoveryQuorumAvailability.NoQuorum,
 $"Only {reachable}/{required} required witnesses were reachable; committed state cannot be corroborated.");

 public static AuthorizationRecoveryQuorumVerifyResult Confirmed() =>
 new(true, AuthorizationRecoveryQuorumAvailability.Available, null);

 public static AuthorizationRecoveryQuorumVerifyResult Mismatch(string reason) =>
 new(false, AuthorizationRecoveryQuorumAvailability.Available, reason);
}

/// <summary>
/// One independent, read-only witness together with a caller-controlled reachability probe. A
/// witness never receives writes; it exists only to corroborate that the primary anchor's state is
/// not being observed from an isolated minority partition. In production a witness is an
/// independent read replica, monitoring agent, or region of the real consensus store — never a
/// second place authority can be written, since already establishes that two independently
/// writable copies of recovery history are exactly the split-brain condition being defended against.
/// </summary>
public sealed class AuthorizationRecoveryQuorumWitness
{
 public AuthorizationRecoveryQuorumWitness(string witnessId, IAuthorizationRecoveryForkAnchor anchor, Func<bool>? isReachable = null)
 {
 WitnessId = !string.IsNullOrWhiteSpace(witnessId) ? witnessId : throw new ArgumentException("Witness id is required.", nameof(witnessId));
 Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
 _isReachable = isReachable ?? (static () => true);
 }

 private readonly Func<bool> _isReachable;

 public string WitnessId { get; }
 public IAuthorizationRecoveryForkAnchor Anchor { get; }
 public bool IsReachable => _isReachable();
}

/// <summary>
/// Quorum/availability boundary in front of an independent recovery-fork anchor (). The safe
/// rule: a majority of witnesses must be reachable and must agree with a caller's expected state
/// before that caller is allowed to attempt the one authoritative write; when witnesses cannot be
/// corroborated, the anchor fails closed instead of guessing. Verification of already-committed
/// state is exposed as a separate read-only operation that degrades independently from the write
/// path and never itself creates new authority.
/// </summary>
public interface IAuthorizationRecoveryQuorumAnchor
{
 /// <summary>
 /// Attempts to advance the shared recovery history. Requires a reachable majority of witnesses
 /// that all currently agree with <paramref name="expectedSequence"/>/<paramref name="expectedDigest"/>
 /// before the underlying anchor's own compare-and-advance is even attempted; otherwise no write
 /// is attempted at all and <see cref="AuthorizationRecoveryQuorumAdvanceResult.Advanced"/> is <c>false</c>.
 /// </summary>
 ValueTask<AuthorizationRecoveryQuorumAdvanceResult> TryAdvanceAsync(
 long expectedSequence,
 string expectedDigest,
 long nextSequence,
 string nextDigest,
 string writerId,
 CancellationToken cancellationToken = default);

 /// <summary>
 /// Confirms, without writing, whether an already-sealed (sequence, digest) pair is still
 /// consistent with what a reachable majority of witnesses currently hold.
 /// </summary>
 ValueTask<AuthorizationRecoveryQuorumVerifyResult> TryVerifyCommittedAsync(
 long sequence,
 string digest,
 CancellationToken cancellationToken = default);
}

/// <summary>
/// Reference/test-only quorum wrapper around the single authoritative anchor.
///
/// Because that anchor is deliberately rollback-resistant (), a write that was partially
/// applied across several independently writable nodes could never be safely undone if a competing
/// writer subsequently won elsewhere — the anchor would simply refuse the compensating "undo" as a
/// rollback attempt. This wrapper therefore never attempts more than one authoritative write: the
/// witness set below is read-only and exists purely to decide whether the caller currently has
/// enough independent corroboration to trust that it isn't operating from an isolated minority
/// partition before it is allowed to touch the single primary anchor at all.
///
/// In production the primary itself must be backed by a real consensus protocol (Raft/Paxos, or an
/// equivalent strongly consistent control plane / KMS-HSM / append-only ledger, per ). This
/// wrapper adds a client-side circuit breaker in front of that; it does not implement distributed
/// consensus itself, and it is not a substitute for the primary's own linearizability.
/// </summary>
public sealed class QuorumAuthorizationRecoveryForkAnchor : IAuthorizationRecoveryQuorumAnchor
{
 private readonly IAuthorizationRecoveryForkAnchor _primary;
 private readonly IReadOnlyList<AuthorizationRecoveryQuorumWitness> _witnesses;
 private readonly int _majority;

 public QuorumAuthorizationRecoveryForkAnchor(
 IAuthorizationRecoveryForkAnchor primary,
 IReadOnlyList<AuthorizationRecoveryQuorumWitness> witnesses)
 {
 _primary = primary ?? throw new ArgumentNullException(nameof(primary));
 if (witnesses is null || witnesses.Count == 0)
 throw new ArgumentException("At least one witness is required.", nameof(witnesses));
 if (witnesses.Select(static w => w.WitnessId).Distinct(StringComparer.Ordinal).Count() != witnesses.Count)
 throw new ArgumentException("Witness ids must be unique.", nameof(witnesses));

 _witnesses = witnesses;
 _majority = (witnesses.Count / 2) + 1;
 }

 public async ValueTask<AuthorizationRecoveryQuorumAdvanceResult> TryAdvanceAsync(
 long expectedSequence,
 string expectedDigest,
 long nextSequence,
 string nextDigest,
 string writerId,
 CancellationToken cancellationToken = default)
 {
 var fallback = new AuthorizationRecoveryAnchorState(expectedSequence, expectedDigest, null);
 var corroboration = await CorroborateAsync(expectedSequence, expectedDigest, cancellationToken);
 if (corroboration.Availability != AuthorizationRecoveryQuorumAvailability.Available)
 return AuthorizationRecoveryQuorumAdvanceResult.NoQuorum(fallback, corroboration.Reachable, _majority);
 if (!corroboration.Agrees)
 return AuthorizationRecoveryQuorumAdvanceResult.Rejected(corroboration.ObservedState ?? fallback, corroboration.Reason!);

 // Exactly one authoritative write is attempted, on the primary anchor only. Its own
 // linearizable compare-and-advance () is what actually decides any remaining race; the
 // witness quorum above only decided whether this caller was allowed to attempt it at all.
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
 var corroboration = await CorroborateAsync(sequence, digest, cancellationToken);
 if (corroboration.Availability != AuthorizationRecoveryQuorumAvailability.Available)
 return AuthorizationRecoveryQuorumVerifyResult.NoQuorum(corroboration.Reachable, _majority);

 return corroboration.Agrees
 ? AuthorizationRecoveryQuorumVerifyResult.Confirmed()
 : AuthorizationRecoveryQuorumVerifyResult.Mismatch(corroboration.Reason!);
 }

 /// <summary>
 /// Reads every reachable witness. Requires a reachable majority, requires the reachable
 /// witnesses to agree with each other, and reports whether they agree with the caller's
 /// candidate (sequence, digest). Never writes anywhere.
 /// </summary>
 private async ValueTask<CorroborationResult> CorroborateAsync(long sequence, string digest, CancellationToken cancellationToken)
 {
 var reachable = _witnesses.Where(static w => w.IsReachable).ToList();
 if (reachable.Count < _majority)
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

 private sealed record CorroborationResult(
 AuthorizationRecoveryQuorumAvailability Availability,
 int Reachable,
 bool Agrees,
 AuthorizationRecoveryAnchorState? ObservedState,
 string? Reason);
}
