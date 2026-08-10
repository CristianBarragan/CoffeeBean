# Current Status

[Home](../README.md) → **Current Status**

## Executive summary

Foundgine has now proven the two halves that matter most:

1. a real provider-neutral planning/execution substrate;
2. a semantic layer that can resolve structured intent and feed that intent into the existing planning/execution path in an end-to-end Banking proof.

The project is **not** at the point where it needs another architectural layer. The next work is to make the semantic-to-query handoff reusable and then measure the system.

> **Freeze the architecture. Prove the bridge. Benchmark it.**

---

## Proven execution substrate

The canonical lower pipeline is:

```text
Domain
   ↓
MetadataRegistry + JoinGraph
   ↓
QueryIntent
   ↓
QueryPlanner
   ↓
QueryPlan
   ↓
SqlPlanCompiler
   ↓
ProviderPlan
   ↓
SqlExecutionProvider
   ↓
real SQLite
   ↓
ExecutionRow
```

### Proven scenarios

| Capability | Status | Proof |
|---|---|---|
| Linear Customer → Account → Transaction | DONE | `BankingEndToEndTests` |
| Branching query tree | DONE | `BankingEndToEndTests` |
| No invented relationship | DONE | negative E2E |
| Ugly physical schema | DONE | `UglySchemaEndToEndTests` |
| Five-entity composite | DONE | `ProductCompositeEndToEndTests` |
| Repeated/self-joined entity occurrences | DONE | `RepeatedEntityEndToEndTests` |
| Filter + sort + paging | DONE | `FilterSortPageEndToEndTests` |
| Create/update/delete | DONE | `MutationEndToEndTests` |
| Atomic multi-entity mutation plan | DONE | `MutationEndToEndTests` |
| Unfiltered update rejection | DONE | `MutationEndToEndTests` |

The five-entity proof is particularly important because the logical `Product` shape spans:

```text
Customer
  ↓
CustomerBankingRelationship
  ↓
Contract
  ↓
Account
  ↓
Transaction
```

There is no `Product` table. The planner discovers the physical chain from registered metadata and joins. The proof therefore demonstrates the core composite-model thesis rather than a special Product implementation.

---

## Semantic layer

The active semantic project is:

```text
src/Foundgine.Semantic
```

It currently contains the semantic model, inference support, resolution and structured read/action intent primitives.

The important active concepts are:

```text
SemanticModel
SemanticEntity
SemanticField
SemanticIdentity
SemanticRelationship
SearchCapability
EntityResolver
ReadIntent
ReadPlanner
ResolvedReadPlan
```

Action and policy descriptors exist, but their complete execution lifecycle is deliberately **not** the current focus.

### Semantic model

The semantic model describes application meaning without knowing about SQL, GraphQL or transport.

It supports:

- entities;
- identities;
- fields;
- relationships;
- relationship cardinality;
- search capabilities;
- inference from existing metadata.

Semantic configuration should add meaning that cannot safely be inferred rather than duplicate the entire metadata model.

---

## Entity resolution

`EntityResolver` supports:

```text
explicit identity
free-text search
relationship lookup
```

Resolution returns:

```text
Resolved
NotFound
Ambiguous
```

and preserves evidence.

The core invariant is:

> **Never silently invent an identity.**

### Important architectural distinction

Resolution and traversal are not the same operation.

Resolution identifies a concrete entity:

```text
"Ada Lovelace"
      ↓
Customer #1
```

Traversal describes a set of related entities:

```text
Customer #1
   ↓ 1:N
Accounts
   ↓ 1:N
Transactions
```

The resolver must not turn a collection-valued relationship into a single identity merely because the current sample happens to contain one row.

This is the next semantic hardening point for the reusable bridge.

---

## Structured read intent

`ReadIntent` is deliberately structured. It is **not** a natural-language parser.

Conceptually:

```text
Anchor:
    Customer / "Ada Lovelace"

Traversal:
    Accounts → Transactions

Order:
    Transaction.Id DESC

Limit:
    5
```

The producer may be:

- an LLM;
- another parser;
- a UI;
- an application;
- a test.

Foundgine owns the constrained representation and safe execution path, not language understanding.

---

## Semantic → execution proof

The current acceptance path proves the complete architecture against a real SQLite database:

```text
Structured ReadIntent
        ↓
EntityResolver / ReadPlanner
        ↓
ResolvedReadPlan
        ↓
QueryIntent
        ↓
QueryPlanner
        ↓
QueryPlan
        ↓
SqlPlanCompiler
        ↓
ProviderPlan
        ↓
SqlExecutionProvider
        ↓
SQLite
        ↓
Result
```

The Banking sample and `ReadIntentEndToEndTests` prove the concrete scenario:

> **Find Ada Lovelace's last five transactions.**

The five-entity `ProductSemanticIntentEndToEndTests` also proves the semantic/planning path on the deeper composite domain and exercises a repeated `Customer` occurrence.

### What is still missing

The final semantic-to-query translation is still expressed in the acceptance path rather than exposed as a dedicated reusable public runtime component.

So the current state is:

```text
Architecture:        PROVEN
End-to-end scenario: PROVEN
Reusable bridge API: NOT YET FROZEN
```

That distinction matters. The next task is productization, not another architectural rewrite.

---

## Current architecture

The active dependency graph is:

```text
Foundation → Abstractions
Metadata → Foundation
Diagnostics → Foundation
Builders → Metadata
Execution.Contracts → Metadata
Semantic → Metadata
Planning → Metadata + Builders
Providers → Builders + Execution.Contracts
```

`Foundgine.Semantic` does not depend on SQL, providers or GraphQL.

`Foundgine.Planning` remains the one logical planner. The semantic layer should translate into its `QueryIntent` rather than introduce a second planner hierarchy.

---

## What is intentionally not complete

The following are not production claims:

- full LLM intent extraction;
- general-purpose agent orchestration;
- production policy engine;
- preview/approval runtime;
- generalized post-execution verification framework;
- generalized evidence pipeline;
- MCP adapter;
- semantic retrieval provider;
- external-data execution;
- Roslyn semantic compiler;
- universal provider support;
- formal Native AOT verification;
- benchmark superiority claims.

Low-level mutation planning and SQLite execution are proven, but the AI-facing semantic action/policy lifecycle remains future work.

---

## Current priority

### P0 — productize the semantic read bridge

Create the smallest reusable production boundary for:

```text
ResolvedReadPlan
        ↓
QueryIntent
```

It must preserve:

- resolved identity;
- relationship traversal;
- filters;
- ordering;
- limits;
- ambiguity/failure state.

It must **not** create a second planner.

### P1 — prove collection traversal

The bridge must distinguish:

```text
resolve one identity
```

from:

```text
traverse a one-to-many relationship
```

The next hard Banking case is:

```text
Ada
 ├── Checking account
 │    └── Transactions
 └── Savings account
      └── Transactions
```

and:

> **Find Ada's five most recent transactions across all her accounts.**

This is a more important proof than adding another semantic feature.

### P2 — benchmark

Measure separately:

- metadata construction;
- semantic resolution;
- read planning;
- query planning;
- provider compilation;
- SQL translation;
- database execution;
- total end-to-end time.

Use linear, branching, composite and repeated-entity shapes.

### P3 — simplify semantic mapping

Use existing metadata for what it already knows:

- identity;
- fields;
- relationships;
- types.

Use semantic configuration for what cannot safely be inferred:

- fuzzy search;
- aliases;
- human-facing descriptions;
- explicit domain meaning.

### P4 — action/policy lifecycle

Only after the read path is clean:

```text
Action intent
 → resolve
 → policy
 → plan
 → preview
 → execute
 → verify
 → evidence
```

Action/policy code already present should remain experimental and should not drive the core architecture yet.

### P5 — MCP

MCP should be a thin adapter over the proven semantic API, not a new core layer.

---

## What should not happen next

Do not start another architecture pass.

Do not add:

```text
LLM orchestration
MCP implementation
AOT compiler
new provider families
Graphgine/GraphQL integration
fuzzy ranking framework
agent framework
second planner hierarchy
```

These remain future/archived directions until the current proof is benchmarked.

---

## Definition of the next success checkpoint

Foundgine reaches the next meaningful checkpoint when this is reusable rather than test-specific:

```text
ReadIntent
   ↓
Resolution
   ↓
Collection-aware traversal
   ↓
QueryIntent
   ↓
QueryPlan
   ↓
ProviderPlan
   ↓
real execution
   ↓
Evidence
```

The project should not add another major subsystem before this path is clean and measured.
