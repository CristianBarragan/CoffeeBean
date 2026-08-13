# Provider independence

Foundgine's provider boundary is an architectural claim, not just an interface.

The core produces a provider-independent `ExecutionPlan`. A provider compiler turns that plan into a provider-specific `ProviderPlan`, and an execution provider runs it.

The repository now contains two structurally different execution paths:

```text
Semantic intent
      ↓
ExecutionPlan
   ↙         ↘
SQL compiler   In-memory compiler
   ↓                ↓
SqlPlan        InMemoryPlan
   ↓                ↓
SQL database   CLR-backed rows
```

The in-memory provider deliberately does not translate the plan into SQL. It resolves relationship traversal through Foundgine metadata and evaluates the supported semantic query operations directly against CLR values.

## What this proves

The important test is not that two providers share an interface. It is that the **same provider-independent execution plan** can be consumed by materially different execution strategies.

That gives Foundgine a stronger architectural statement:

> The execution plan describes application semantics, not SQL.

## Scope

`Foundgine.InMemory` is intentionally a proof provider. It currently covers:

- entity scans
- relationship traversal
- field projection
- equality, inequality, and `IN` filters
- AND/OR filters
- root ordering
- offset/limit pagination
- the supported context-based authorization predicate form

It does not claim complete parity with the SQL provider yet. Full provider-equivalence testing is a later milestone.

## Why not another SQL database?

A second relational database would still allow an implementation to hide SQL-shaped assumptions. The in-memory provider removes SQL entirely from the execution path, making it a better architectural test.
