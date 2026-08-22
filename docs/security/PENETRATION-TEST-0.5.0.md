# Foundgine 0.5.0 — Authorization Penetration Test

## Scope

This penetration pass targets the high-assurance `TransferFunds` consequential mutation and its PostgreSQL execution boundary.

The attacker model assumes that an upstream authorization dependency can be compromised or can return an overly permissive `ALLOW` decision. The execution boundary must still enforce security invariants that are intrinsic to the mutation.

## Attacks covered

| ID | Attack | Expected result |
|---|---|---|
| PT-001 | Compromised authorizer attempts transfer from an account not owned by actor | Denied; no state mutation |
| PT-002 | Single transfer exceeds source daily limit | Denied; no state mutation |
| PT-003 | Batch splits transfers to exceed source daily limit | Denied; entire transaction rolled back |
| PT-004 | Caller supplies a tenant different from account tenant | Denied; no state mutation |
| PT-005 | Compromised authorizer attempts to bypass daily limit | Denied; no state mutation |
| PT-006 | Authorization evidence changes before commit | Denied; existing regression coverage |
| PT-007 | Idempotency replay with altered request | Denied; existing regression coverage |
| PT-008 | Frozen-account mutation | Denied; existing regression coverage |

## Findings fixed by this pass

### PT-F001 — PostgreSQL execution did not independently enforce ownership

**Severity:** High

The PostgreSQL executor previously delegated ownership entirely to `_authorize`. A compromised or incorrectly implemented authorization callback could return `ALLOW` for an actor who did not own one or both accounts.

**Fix:** `ValidateExecution` now independently requires the actor to own both source and destination accounts before mutation.

### PT-F002 — PostgreSQL execution did not enforce the source daily limit

**Severity:** High

The semantic capability declared the daily-limit invariant, and the in-memory implementation enforced it, but the PostgreSQL executor's execution validator did not. A transfer could therefore exceed the declared daily limit.

**Fix:** Single-transfer execution now enforces `daily_transferred + amount <= daily_limit` while holding the account row locks.

### PT-F003 — PostgreSQL batch execution could split a daily-limit violation across commands

**Severity:** High

Per-command validation is insufficient for a batch because several commands can debit the same source account. The aggregate outgoing amount must be checked against the source's remaining daily limit.

**Fix:** Batch execution now aggregates outgoing amounts by source account and rejects the complete transaction when the aggregate exceeds the source daily limit.

## Security boundary after the fix

The PostgreSQL mutation now requires all of the following before commit:

- tenant isolation;
- source ownership;
- destination ownership;
- authorization decision is allowed;
- authorization evidence is valid and unchanged;
- authoritative authorization context matches when configured;
- neither account is frozen;
- source daily limit is respected;
- available funds are sufficient;
- idempotency key is serialized;
- mutation, idempotency and audit are committed atomically.

## CI requirement

These tests belong to the security/integration gate and must fail CI if an attack succeeds or if state changes after a denied attack.
