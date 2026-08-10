# Semantic Resolution and Traversal

[Home](../../README.md) → [Architecture](README.md) → **Semantic Resolution and Traversal**

This document records an important architectural rule discovered while exercising the semantic layer against the Banking proof.

## Resolution

Resolution identifies a concrete entity.

```text
"Ada Lovelace"
      ↓
Customer #1
```

It is appropriate for:

- explicit IDs;
- unique names;
- searchable identity fields;
- references that should resolve to one domain object.

The resolver must return `Ambiguous` when the evidence does not identify one object safely.

## Traversal

Traversal describes how a query walks the domain graph.

```text
Customer #1
   ↓ 1:N
Accounts
   ↓ 1:N
Transactions
```

A collection-valued relationship does not need to resolve to one Account before the planner can continue.

## Why this matters

A request such as:

> Find Ada's five most recent transactions across all her accounts.

should mean:

```text
Resolve:
    Ada → Customer #1

Traverse:
    Customer #1
      → Accounts*
      → Transactions*

Plan:
    ORDER BY Transaction.Id DESC
    LIMIT 5
```

It must not mean:

```text
Resolve Ada
   ↓
choose one Account
   ↓
resolve Transactions from that one Account
```

The latter silently loses valid data whenever the customer has more than one account.

## Architectural consequence

`EntityResolver` remains responsible for identity resolution.

`ReadPlanner`/semantic translation describes relationship traversal.

`Foundgine.Planning.QueryPlanner` remains responsible for turning the resulting structured query intent into a provider-neutral logical plan.

The intended pipeline is:

```text
ReadIntent
   ↓
identity resolution
   ↓
collection-aware traversal
   ↓
QueryIntent
   ↓
QueryPlanner
   ↓
QueryPlan
```

There is no need for a second semantic/physical planner hierarchy.

## Acceptance scenario

The next hard proof is:

```text
Ada
 ├── Checking → Transactions
 └── Savings  → Transactions
```

with:

> Find Ada's five most recent transactions.

The expected result must consider transactions from both accounts.

This test should use the existing metadata, join graph and SQL provider machinery rather than introduce a special-case resolver.
