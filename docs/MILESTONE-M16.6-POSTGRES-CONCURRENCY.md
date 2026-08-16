# M16.6 — PostgreSQL Concurrency + Adversarial Execution

M16.6 is the hostile-execution gate for the high-assurance mutation path.

The goal is not to prove that Foundgine makes business policy correct. The goal is to prove that a correctly defined mutation contract remains enforced when PostgreSQL is subjected to concurrent and adversarial requests.

## Attack surface

- duplicate idempotency keys
- opposing account transfers
- cross-tenant account pairs
- frozen accounts
- execution-time authorization denial
- raw-balance versus available-funds confusion

## Required guarantees

1. The same idempotency key produces exactly one committed transfer.
2. A -> B and B -> A use deterministic row-lock ordering and complete without deadlock.
3. Tenant mismatch is rejected before any state transition is committed.
4. Frozen accounts cannot participate in a transfer.
5. Authorization is evaluated after the locked current state has been loaded.
6. Available funds are computed from the defined semantic components rather than raw balance.
7. Failed execution leaves balances, idempotency, and audit state unchanged.

## Test execution

Set `FOUNDGINE_POSTGRES_CONNECTION` to a PostgreSQL 17 connection string and run the dedicated test project.

These tests intentionally use a real PostgreSQL database. They should not be replaced by an in-memory substitute when evaluating the concurrency guarantees.

## What this does not prove

M16.6 does not establish that the domain policy is semantically correct, that authentication is secure, or that every possible isolation-level anomaly has been eliminated. It establishes a concrete hostile-execution contract around the current `TransferFunds` capability.
