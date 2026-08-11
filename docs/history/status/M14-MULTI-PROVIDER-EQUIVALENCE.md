# M14 — Multi-Provider Equivalence

## Purpose

M14 proves the provider boundary with two independent consumers of the same
provider-independent `ExecutionPlan`.

The production SQL provider is compared with a test-only reference provider.
The reference provider deliberately lives under `tests/` and is not part of
the Foundgine product surface. Its purpose is to prove that the logical plan
can be consumed without SQL or SQLite concepts.

## Proof

```text
SemanticRequest
      ↓
Resolution
      ↓
Authorization
      ↓
ExecutionPlan
      ├──────────────→ SqlCompiler → SqlPlan → SQLite
      │
      └──────────────→ ReferenceCompiler → ReferencePlan → reference execution
```

Both providers receive the same `ExecutionPlan` instance.

The acceptance test uses the Banking scenario with a relationship filter:

```text
Transaction
  └─ Account
       └─ Customer.Name = "Alice"
```

and root ordering by `TransactionDate DESC`, limited to five rows.

Both providers must return the same root transaction identities:

```text
106, 105, 104, 103, 102
```

## What this proves

- Semantic resolution is provider-independent.
- Authorization is provider-independent.
- Planning is provider-independent.
- SQL is not required to interpret the semantic request.
- A second execution strategy can consume the same logical plan.

## What this does not prove

The reference provider is intentionally minimal. It is not a second production
database implementation and does not claim feature parity with SQL.

The next provider should only be added if a real product requirement exists.
Do not add PostgreSQL merely to satisfy a milestone.

## Decision

M14 is an architectural acceptance test, not a commitment to maintain a second
provider implementation today.
