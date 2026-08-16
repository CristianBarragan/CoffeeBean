# M16 — High-Assurance Mutation: TransferFunds

## Purpose

M16 is the first consequential business mutation proof. It deliberately tests a semantic action whose correctness cannot be reduced to a CRUD update.

The flagship capability is:

`BankAccount.transferFunds`

The sample lives in `samples/Foundgine.HighAssurance.Banking`.

## Trust boundary

```text
Interpreted intent
       |
       v
TransferFundsCommand
       |
       v
Semantic capability contract
       |
       v
Execution-time authorization + invariant validation
       |
       v
Atomic state transition
       |
       +--> audit
       +--> idempotency
       +--> execution receipt
```

The sample intentionally does not claim that an LLM can infer the definition of available funds. The business definition is explicit:

```text
available_funds = balance - pending_transactions - regulatory_hold
```

## Required invariants

- source and destination are different
- request tenant matches both accounts
- actor owns both accounts
- neither account is frozen
- amount is positive
- available funds cover the amount
- daily transferred amount plus the transfer does not exceed the source limit
- an idempotency key is mandatory
- replay returns the original result and does not apply a second transfer
- an idempotency key cannot be rebound to a different actor, tenant, account pair, or amount
- debit and credit happen as one logical state transition
- a consequential audit event is emitted
- an execution receipt identifies the capability, plan and authorization boundary

## Concurrency

The reference sample locks both accounts in deterministic identifier order. This prevents two concurrent transfers from independently observing the same mutable source state in the in-memory proof and avoids lock-order deadlocks.

This is a domain-level proof, not a replacement for database transactions. The PostgreSQL implementation gate must map these invariants to a real database transaction with row-level locking or equivalent concurrency control.

## What this proves

M16 proves that Foundgine's semantic boundary can host an explicit, stateful business capability without pretending that generic mutation planning is the business policy itself.

It does **not** prove:

- financial correctness of an arbitrary banking domain
- inference correctness of an LLM
- PostgreSQL transaction isolation behavior
- production-grade distributed idempotency
- external audit-system atomicity

Those remain separate validation gates.
