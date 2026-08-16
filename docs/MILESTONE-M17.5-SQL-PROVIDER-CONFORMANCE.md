# M17.5 — SQL Provider Security Conformance

## Purpose

M17.5 turns the M17.4 provider attestation into a provider-specific conformance gate.

The SQL provider now performs structural checks against the compiled SQL plan when security invariants are required.

## What is checked

- authorization invariants require compiled authorization predicates;
- runtime authorization keeps authorization context values as parameter bindings;
- semantic values have explicit SQL parameter bindings;
- field visibility has an explicit projected column set;
- relationship visibility has an explicit execution/projected shape;
- plan-cache context isolation rejects request-specific context values embedded in SQL text.

## What is deliberately not inferred

Mutation guarantees such as atomicity, idempotency, replay protection, audit and execution receipts cannot be proven from an ordinary `SqlPlan`. They require the high-assurance mutation provider contract and transactional integration tests.

Likewise, structural conformance is not a proof of PostgreSQL correctness. It is a fail-closed provider gate that verifies concrete evidence exposed by the provider plan.

## Security progression

```text
M17.3  Security vocabulary
        ↓
M17.4  Plan-level invariant contract
        ↓
M17.5  Provider-specific conformance
        ↓
M18    Security-aware optimization and rewrite preservation
```

The central rule is:

> A provider must not merely declare that it preserves an invariant; its compiled plan must expose the evidence required for that invariant, or execution is rejected.
