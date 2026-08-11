# M9 — Structured Intent Acceptance

## Purpose

Prove the product thesis with a vertical slice that does not depend on GraphQL
or an LLM provider.

The producer supplies a small, semantic, structured read intent:

```text
Transaction
  fields: Id, Amount, TransactionDate
  filter: Account → Customer → Name = "Alice"
  order: TransactionDate DESC
  limit: 5
```

Foundgine then performs the normal pipeline:

```text
Structured ReadIntent
        ↓
ReadIntentCompiler
        ↓
SemanticRequest
        ↓
Resolve
        ↓
Authorize
        ↓
ExecutionPlan
        ↓
SQL provider
        ↓
SQLite
```

## What this proves

- A non-GraphQL producer can use the same semantic engine.
- The producer does not need SQL, table names, join conditions, or ORM expressions.
- Intent names are resolved against the static semantic model before planning.
- Invalid semantic names fail before planning or execution.
- Relationship-aware filters remain semantic concepts.
- Ordering and limits remain provider-neutral until SQL compilation.

## What this does not prove

This is not an LLM integration and does not attempt natural-language parsing.
The external model/parser remains responsible for turning language into the
structured `ReadIntent`.

It also does not claim that Foundgine is universally better than EF Core. The
value proposition remains conditional on applications needing a shared,
provider-independent semantic execution boundary.

## Acceptance example

The E2E test represents:

> Find Alice's five most recent transactions.

The test executes seven Alice transactions and one Bob transaction, then proves
that exactly five Alice transactions are returned in descending transaction-date
order.
