# M16.5 — PostgreSQL High-Assurance Execution

M16 proved the semantic `TransferFunds` contract in memory. M16.5 carries the same contract across the real PostgreSQL execution boundary.

## Trust boundary

```text
Structured capability intent
        |
        v
Semantic invariants
        |
        v
Execution-time authorization
        |
        v
PostgreSQL transaction
        |
        +-- idempotency-key advisory lock
        +-- deterministic account row locks
        +-- re-read account state
        +-- re-check tenant/frozen/limits/available funds
        +-- debit source
        +-- credit destination
        +-- persist idempotency result
        +-- persist audit event
        |
        v
COMMIT
```

## Guarantees demonstrated

- The source and destination rows are locked before the consequential state transition.
- Account locks are acquired in deterministic ID order to prevent A→B/B→A deadlocks.
- The same idempotency key is serialized with a PostgreSQL transaction advisory lock.
- Idempotency is checked before any mutation and the original result is returned on replay.
- The authorization function receives the locked, current account state immediately before mutation.
- Tenant, frozen-account, daily-limit and available-funds invariants are checked from current database values.
- Debit, credit, idempotency persistence and audit persistence share one transaction.
- Any exception rolls the complete operation back.
- The receipt records the PostgreSQL execution provider and a plan fingerprint.

## Important limitation

This milestone does not claim that PostgreSQL permissions, network security, authentication, secret management, deployment security, or the correctness of the authorization/business policy are automatically solved.

It proves that once the capability contract and policy are defined, Foundgine can carry them into a real transactional provider without weakening the semantic boundary.

## Next gate

The next security/performance gate should exercise concurrent transfers against the same and opposing account pairs, then run the adversarial agent harness against the PostgreSQL provider. Measurements should include lock contention, p50/p95/p99 latency, rollback behavior, idempotency races, and plan/authorization overhead.
