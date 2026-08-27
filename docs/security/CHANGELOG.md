# Security invariant history

Invariant-by-invariant history of the authorization recovery control plane (`src/Foundgine.Security.Authority/Recovery/`), in reverse-ish chronological order as originally recorded. Section headers use plain descriptive titles rather than internal tracking IDs; the itemized per-release change list lives in the root [`CHANGELOG.md`](../../CHANGELOG.md).

## Repair Proposer Authentication Binding Security

- Bound repair proposer credentials to exact transaction identity and durable state.
- Added credential sequence/fingerprint lifecycle fencing and HMAC proof validation.
- Added fail-closed transaction/state substitution, stale credential, and forged-proof tests.
- Added 32-concurrent authorization adversarial coverage.

## Authorization Recovery Control-Plane Repair Ordering, Idempotency & Transaction-Identity Collision Security

### Added

- `AuthorizationRecoveryControlPlaneRepairOrdering`
- strict monotonic repair revision ordering
- exact transaction identity binding
- idempotent replay of an identical committed transaction
- transaction-ID collision rejection when immutable payload differs
- stale-plan rejection after a competing commit
- revision-skip rejection
- 64-concurrent repair race coverage

### Security invariants

- transaction IDs are not sufficient authorization to mutate state;
- identical committed transactions are idempotent;
- a reused transaction ID with different immutable fields fails closed;
- repair commits advance the durable revision exactly once;
- stale plans cannot be inserted behind newer durable state;
- repair ordering remains outside semantic/provider/compiled-plan cache identity.

### Validation

ZIP integrity was verified after packaging.

The required .NET runtime/compiler is unavailable in the validation environment. Therefore no claim is made that .NET/PostgreSQL integration tests executed.

# Transaction Journal Integrity & Anti-Tamper Recovery Security

# Cross-Instance Recovery Verification Concurrency Security

# Key Retirement & Recovery Verification Concurrency Security

# Key Rotation Publication Atomicity & Verification Continuity

Authorization Recovery Control-Plane Publication Key Lifecycle & Rotation Security.

Authorization Recovery Control-Plane Durable Publication Integrity Security.

Authorization Recovery Control-Plane Promotion Recovery & Incomplete-Commit Reconciliation Security.

Authorization Recovery Control-Plane Promotion Commit/Publish Atomicity Security.

Authorization Recovery Control-Plane Promotion Atomicity & Concurrent Promotion Security.

Authorization Recovery Control-Plane Standby Synchronization & Promotion Safety Security.

# Authorization Recovery Proposer Credential Revocation Audit & Tamper-Evident History Security.

## Authorization Recovery Proposer Credential Compromise & Revocation Propagation Security

Explicit proposer credential revocation, monotonic generation invalidation, and lease-serialized compromise handling.
# Authorization Recovery Reconfiguration Durable Persistence & Cross-Instance Reconciliation Security

- Added `IAuthorizationRecoveryReconfigurationLedgerStore` as the durable persistence boundary for the reconfiguration audit ledger.
- Added an atomic witness-id membership manifest alongside each durable ledger record; runtime witness handles remain non-persistent.
- Added fail-closed `BootstrapAsync` and `RestoreAsync` reconciliation.
- Startup now verifies the complete hash chain, every membership manifest, and the live configuration against the durable ledger head.
- Durable reconfiguration is persisted before the new membership becomes live.
- Added stale/divergent-instance and tamper/missing-manifest adversarial tests.
- Added an in-memory reference store explicitly scoped to tests; production requires durable tamper-resistant persistence.
- Kept recovery configuration and audit history outside semantic/provider-plan/compiled-plan cache identity.

## Authorization Recovery Reconfiguration Audit Trail Security

- Added a tamper-evident, hash-chained audit ledger for witness-set reconfiguration: every accepted
  reconfiguration, including the genesis membership, is recorded with a digest chaining to the record
  before it.
- Added a static `VerifyChain` that checks version continuity, previous-digest chaining, and per-record
  digest recomputation, independent of any live anchor — a reconstructed ledger from durable storage
  verifies exactly like the in-process one.
- Refused reconfiguration attempts are never recorded; only accepted membership changes extend the
  ledger.
- Added optional proposer-id attribution to reconfiguration proposals.
- Added adversarial tests for tampered, deleted, reordered, and forged ledger records, plus
  order-independence of the membership digest.

## Authorization Recovery Witness Set Reconfiguration Security

- Added safe witness-set reconfiguration in front of the recovery-anchor quorum boundary: membership changes now
  require a reachable majority of the CURRENT witnesses before they are accepted.
- Made reconfiguration monotonically versioned and CAS-gated so concurrent proposals cannot both apply.
- Ensured a superseded witness configuration can never again satisfy quorum once a newer one commits,
  even if its witnesses remain fully reachable and in agreement with each other.
- Rejected empty or duplicate-id proposed witness memberships.
- Fixed the shared recovery-anchor genesis digest, which previously made genesis unreachable through
  the public API; updated one pre-existing test assertion that relied on the old behavior.
- Added adversarial reconfiguration tests and documentation.

## Authorization Recovery Quorum & Anchor Availability Security

- Added a witness-quorum availability boundary in front of the recovery anchor.
- Enforced no-quorum/no-new-authority: no write is attempted anywhere without a reachable majority.
- Kept the authoritative write to a single compare-and-advance call rather than a multi-node
  write-with-compensation design, which would conflict with the anchor's own rollback resistance.
- Added an independent read-only verification path for already-committed checkpoints that reports
  quorum loss as indeterminate rather than success.
- Added adversarial partition, quorum-recovery, disagreement, and concurrency tests and documentation.

## Authorization Recovery Anchor Fork & Split-Brain Security

- Added recovery-anchor compare-and-advance bound to sequence and state digest.
- Added split-brain/fork detection across concurrent application instances.
- Added fail-closed behavior when no shared linearizable anchor exists.
- Added adversarial recovery fork security tests and documentation.

# Authorization Delegation Chain State-Machine & Linearizability Security

- Added an explicit provider-neutral delegation lifecycle state machine.
- Made revoke/compromise/key-rotation transitions linearizable through one per-warrant serialization boundary.
- Made revoked and compromised authority terminal for delegation.
- Added monotonic transition sequencing and stale-observation tests.
- Added concurrent transition adversarial coverage.
- Kept lifecycle state and sequences outside semantic/provider-plan/compiled-plan cache identity.

# Authorization Evidence Freshness & Replay Security

- Added explicit temporal authorization-evidence policy with maximum age, maximum lifetime, and clock-skew bounds.
- Added fail-closed validation for expired, stale, future-dated, and malformed evidence.
- Added canonical temporal binding for actor, tenant, authorization version, issued-at, and expiration.
- Added adversarial replay/tampering tests.
- Kept temporal authorization state outside semantic/provider-plan/compiled-plan cache identity.

# Authorization Key Lifecycle & Rotation Security

- Added authorized external integrity-key lifecycle management.
- Added atomic immutable key-ring rotation with monotonic provenance.
- Added safe retirement guards for persisted authorization evidence.

## Authorization Evidence Atomicity

- Added a PostgreSQL-backed authoritative authorization-context store.
- Locked authorization evidence with `SELECT ... FOR UPDATE` for the complete mutation transaction.
- Bound authorization version/fingerprint validation to the same PostgreSQL serialization domain as mutation commit.
- Added fail-closed behavior when a configured authoritative authorization context is missing.
- Added single-transfer and batch race tests proving authorization changes cannot commit between the final evidence check and mutation commit.

## Security-Context TOCTOU Closure

- Added versioned `AuthorizationDecision` evidence with explicit authorization result, version, and fingerprint.
- Added execution-time evidence revalidation after authoritative row locking.
- Added a commit-time authorization evidence gate for single transfers and batches.
- Added rollback tests proving stale or changed authorization evidence cannot commit balances, idempotency, or audit state.
- Kept authorization evidence outside semantic/provider-plan cache identity.

## Concurrency / Fault-Interleaving Security Closure

- Added concurrent fault-interleaving tests for same-key idempotency and opposing transfers.
- Verified waiting transactions cannot observe rolled-back mutation state.
- Verified rollback releases advisory/row locks and the surviving transaction executes exactly once.

## Provider Fault-Injection Security Closure

- Added PostgreSQL high-assurance mutation fault-injection seam after mutation and before commit.
- Added single-transfer rollback test covering balances, idempotency, and audit.
- Added batch rollback test proving the complete batch is atomic under provider failure.

## Cancellation / Interruption Security

- Added cancellation-aware mutation execution boundary.
- Propagated `CancellationToken` through `FoundgineMutationEngine`.
- Added provider command cancellation and pre-commit cancellation checks.
- Owned SQL transactions roll back on cancellation.
- Added security documentation.

## Timeout / Deadline Security Closure

- Added absolute UTC execution deadlines to `ExecutionContext`.
- Added fail-closed deadline checks before and after provider execution.
- Linked execution deadlines to provider cancellation tokens for reads and mutations.
- Preserved plan/cache semantics by keeping deadlines out of semantic plan identity.
- Added security documentation.

## Transaction Isolation / Database Visibility Security

- High-assurance PostgreSQL TransferFunds now explicitly uses `READ COMMITTED` transactions.
- Added `mutation.transaction.read-committed-isolation` to the provider security contract.
- Added contract and PostgreSQL integration tests for the actual transaction isolation setting.
- Documented the visibility/security argument: correctness relies on deterministic row locking and explicit transaction isolation rather than ambient database defaults.

## Database Visibility Race Security

- Added PostgreSQL integration tests for ownership and frozen-state changes queued ahead of transfer lock acquisition.
- Proved that execution-time authorization/state validation observes committed post-lock state.
- Added security documentation.

- Added authorization-context race security coverage proving authorization is evaluated after authoritative row-lock acquisition.

## Authorization Context Lifecycle Security

- Added an authoritative authorization-context lifecycle contract for PostgreSQL.
- Authorization identities `(actor_id, tenant_id)` are immutable; reassignment requires an explicit delete/create lifecycle.
- Enforced positive, strictly monotonic authorization versions.
- Added lifecycle tombstones so a deleted authorization identity cannot replay an old version during recreation.
- Enforced non-empty authorization fingerprints in both application code and PostgreSQL constraints.
- Added fail-closed handling for missing authoritative authorization context when the store is configured.
- Added lifecycle race tests covering deletion serialization, version replay, identity separation, and context recreation.
- Kept lifecycle state outside semantic/provider-plan cache identity.

## Authorization Context Provenance Security

- Added registered authorization-context writer provenance.
- Bound writers to immutable actor/tenant scope and PostgreSQL `current_user` role.
- Added credential fingerprint validation and monotonic writer write sequences.
- Serialized writer updates with `FOR UPDATE`.
- Added fail-closed tests for cross-tenant writes, actor impersonation, inactive writers, forged credentials, stale writers, and concurrent writers.
- Kept writer provenance outside semantic/provider-plan cache identity.

## Authorization Context Cryptographic Integrity

- Added external-key HMAC-SHA256 integrity protection for persisted authorization context.
- Bound canonical authorization payloads to actor, tenant, allowed state, version, fingerprint, algorithm version, and key id.
- Added constant-time integrity verification and fail-closed behavior for tampered or unknown evidence.
- Added integrity protection to lifecycle tombstones so deletion/recreation state cannot be modified without detection.
- Added verification-key rings with active-key rotation support.
- Added PostgreSQL constraints for integrity algorithm, key id, and tag shape.
- Added adversarial coverage for state tampering, identity tampering, unknown keys, algorithm confusion, tombstone tampering, key rotation, and canonicalization ambiguity.
- Kept cryptographic keys outside PostgreSQL and outside semantic/provider-plan cache identity.

## Authorization Decision Binding & Execution TOCTOU Security

Added execution-time binding between authorization evidence and the exact transfer mutation, with final decision/rebinding checks under the existing PostgreSQL authorization row lock. Added adversarial substitution tests and milestone documentation.

## Authorization Delegation & Attenuation Security

- Cryptographically bound delegated warrants to their parent digest.
- Added ordered delegation-path binding and cycle detection.
- Enforced exact one-level delegation depth and a maximum depth of 32.
- Preserved issuer/audience invariants while allowing an explicit delegated subject.
- Hardened attenuation against capability, tenant, resource, limit, and expiry expansion.
- Added adversarial delegation tests.

## Authorization Delegation Revocation & Cascade Security

- Added monotonic execution-time warrant revocation store.
- Revoked parent digests invalidate delegated descendants.
- Added revocation sequence snapshots for final execution TOCTOU checks.
- Added adversarial cascade, sibling-isolation, resurrection, and concurrency tests.

## Authorization Trust-Boundary Consistency & Key/Policy TOCTOU Security

Added versioned issuer trust snapshots, atomic trust/key lifecycle sequencing, trust fingerprint binding, explicit delegation key lifecycle states, and final execution-time trust-state validation. Verification-only and retired issuer keys cannot authorize new delegations.

## Authorization Delegation Chain Integrity & Path-Binding Security

- Added complete ordered delegation-chain validation.
- Enforced parent-id, parent-digest, issuer, depth, and path continuity for every delegation edge.
- Rejected path splicing, reordering, truncation, sibling substitution, root substitution, and repeated ancestors.
- Added deterministic length-prefixed chain digest computation.
- Kept chain integrity evidence outside semantic/provider/compiled-plan cache identity.
- Added adversarial delegation-chain security tests.

## Authorization Delegation Chain Revocation & Compromise Propagation Security

Added path-bound delegation subtree compromise propagation, sibling isolation, compromised-key isolation, monotonic compromise sequencing, and execution-time TOCTOU checks.

## Authorization Delegation Chain Concurrency & Fork-Consistency Security

Added per-parent optimistic delegation sequencing, stale-writer rejection, duplicate child/nonce protection, parent-bound concurrency snapshots, and adversarial concurrent delegation tests.

## Authorization Delegation Atomic Commit & Cross-Store Consistency Security

Authorization/delegation security transitions now have an explicit PostgreSQL atomic-commit boundary. Multi-write security transitions either commit completely or roll back completely, with publication of transition results only after commit. Added rollback/commit integration coverage.

## Authorization Recovery, Crash Consistency & Durable-State Reconciliation Security

- Added durable recovery checkpoint and fail-closed post-restart verification.
- Added deterministic authorization-state digest and adversarial recovery tests.

## Authorization Recovery Witness Credential Authentication Security

Witness identities used by recovery quorum membership must now be authenticated before they are resolved into live runtime witnesses. Credential identity, fingerprint, and version are checked; the durable ledger continues to store only witness IDs and never credential secrets.

Authorization Recovery Reconfiguration Proposer Authentication Security: proposer identity is now authenticated and authorized, with credentials bound to the expected configuration version and exact proposed membership digest. Missing, forged, stale, or operation-substituted credentials fail closed.

## Authorization Recovery Reconfiguration Proposer Credential Lifecycle & Rotation Security

Proposer credentials now have explicit lifecycle states and monotonic generations. Rotation invalidates prior credentials, verification-only/retired credentials cannot authorize new reconfiguration, and an in-flight reconfiguration holds a lifecycle lease through durable commit to close the authentication-to-commit TOCTOU boundary.

## Authorization Recovery Proposer Credential Revocation Persistence & Cross-Instance Propagation Security

Added authoritative shared proposer credential lifecycle persistence, monotonic sequence CAS, cross-instance revocation/rotation propagation, and a final authoritative lifecycle check before durable reconfiguration publication. Added adversarial stale-instance, restart-resurrection, sequence-rollback, and in-flight revocation tests.

## Authorization Recovery Proposer Credential Audit Head Anchoring & History Rollback Security

Added an independent rollback-resistant proposer-credential audit-head anchor bound to sequence and digest, fail-closed detection of older/future/divergent histories, compare-and-advance concurrency protection, anchored restoration checks, and adversarial rollback/fork/stale-writer tests. The reference anchor is test-only; production requires an independent durable linearizable control-plane trust root.

## Authorization Recovery Proposer Credential Audit Anchor Availability & Partition Security

Authorization Recovery Control-Plane Health, Failover & Recovery Security. Added authority-epoch based failover requiring exact continuity with the externally anchored audit history, with fail-closed protection against independent successor trust roots, rollback, forks, and concurrent successor activation.

Authorization Recovery Control-Plane Failback & Split-Brain Rejoin Security.

## Cross-Instance Commit Atomicity & Crash Consistency

This closes the durable transaction boundary after the prior distributed compare-and-swap work. Recovery, retirement, publication, and the durable revision are now modeled as one atomic state transition. Crash-before-write tests require the complete previous state to remain unchanged, and concurrent recovery commits allow only one decision to cross a given revision.

## Commit Recovery After Crash / Durable Transaction Reconciliation

Added the durable transaction reconciliation boundary for authorization recovery control-plane commits. Prepared transactions are safely discarded after crash when the durable state is unchanged; committed transactions are acknowledged without replay when the durable commit marker and target revision agree. Unresolved transactions fence competing writers until authoritative reconciliation completes. Added adversarial tests for crash timing, exactly-once reconciliation, tampered publications, and 32 concurrent instance attempts. No .NET/PostgreSQL execution is claimed because the required runtime/compiler is unavailable in the validation environment.

## Journal Consensus, Divergence Detection & Recovery Security

Added cross-instance authenticated journal comparison, durable revision/state fingerprint binding, journal-head divergence detection, stale-replica fencing, fail-closed recovery when authoritative history cannot be established, and adversarial concurrent divergence tests. No .NET/PostgreSQL execution is claimed because the required runtime/compiler is unavailable in the validation environment.

## Authoritative Journal Reconciliation & History Repair Security

Added explicit authoritative-history reconciliation for stale control-plane replicas. Repair plans are generated only when the local authenticated journal is a strict prefix of the authoritative history; divergent forks, tampered journals, and state mismatches fail closed. Repair is bounded to the authoritative revision/head and must be applied through the existing durable compare-and-swap transaction boundary. No .NET/PostgreSQL execution is claimed because the required runtime/compiler is unavailable in the validation environment.

## Repair Commit Concurrency & Stale Repair-Plan Invalidation Security

Added exact local/authoritative revision and journal-head binding for repair plans, dual-boundary compare-and-swap repair commits, stale-plan invalidation when either side changes, repair transaction replay protection, exactly-once revision advancement, crash/reconciliation coverage, and adversarial concurrent repair races. No .NET/PostgreSQL execution is claimed because the required runtime/compiler is unavailable in the validation environment.

## Repair Proposer Credential Lifecycle, Rotation, Revocation & In-Flight Concurrency Security

- Atomic proposer credential lifecycle fence.
- Monotonic credential sequence rotation.
- Fail-closed revocation/retirement.
- In-flight authorization/lifecycle concurrency protection.
- Transaction/state/proof binding remains intact across rotation.

## Cross-Instance Repair Proposer Credential Lifecycle Replication & Revocation Propagation Security

- Authoritative cross-instance credential lifecycle replication.
- Monotonic sequence compare-and-set fencing across instances.
- Stale-replica authorization rejection and authoritative lifecycle leases.
- Cross-instance rotation/revocation propagation and stale-writer fencing.
- 32-concurrent adversarial authorization/lifecycle coverage.

## Cross-Instance Repair Proposer Credential Replication Integrity, Ordering & Partition/Recovery Security

- Authenticated lifecycle replication with dedicated HMAC integrity protection.
- Monotonic sequence and previous-digest chain fencing.
- Duplicate replay idempotency and divergent fork rejection.
- Authority-epoch fencing for partition/recovery and stale-state resurrection.
- 32-concurrent adversarial replication coverage.

## Cross-Instance Repair Proposer Credential Source Trust-Key Rotation & Revocation Security

- Added explicit `SourceKeyId` binding to replication envelopes.
- Added per-source trust-key lifecycle state: `Active`, `VerificationOnly`, `Revoked`.
- Added atomic source-key rotation, in-flight verification continuity, terminal revocation, unknown-key rejection, and stale-rotation fencing.
- Preserved the prior ordering, chain, replay, fork, epoch, concurrency, and source-authentication controls.

## Cross-Instance Repair Proposer Credential Replication Source Authentication & Trust-Binding Security

- Added explicit source-instance trust registration.
- Bound replication integrity proofs to source identity and source-specific keys.
- Rejected untrusted sources and source-identity spoofing.
- Preserved the prior ordering, chain, replay, fork, epoch, and concurrency protections.
- Added adversarial source-authentication coverage.

## Witness Credential Lifecycle Replication & Crash Recovery Security

- Added durable-safe witness credential lifecycle replication history.
- Added monotonic lifecycle revisions and SHA-256 previous-digest chaining.
- Added contiguous-history enforcement, duplicate idempotency and divergent-revision rejection.
- Added crash-recovery packages with complete-history replay and stale revoked-state fencing.
- Ensured witness credential secret material is never replicated.
- Added adversarial rotation, revocation, gap, fork, digest-tampering and recovery coverage.

## Witness Credential Lifecycle Replication Source Authentication Security

- Added source-authenticated witness lifecycle replication envelopes.
- Bound source identity, source key identity and the complete lifecycle record to an HMAC-SHA256 proof.
- Added source trust-key rotation, verification-only overlap and terminal revocation.
- Rejected untrusted sources, unknown keys, revoked keys and source-identity spoofing.
- Preserved the prior contiguous-history, previous-digest, replay and divergent-history fencing.
- Added all-or-nothing authenticated recovery verification.
- Fixed the prior recovery atomicity so failed recovery packages cannot partially mutate the journal.
- Added adversarial source-authentication and trust-key lifecycle tests.
- No .NET test execution is claimed because the required runtime/compiler is unavailable in the validation environment.
