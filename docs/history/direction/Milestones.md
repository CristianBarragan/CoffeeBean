# Proof Milestones

[Home](../../README.md) → [Direction](README.md) → **Milestones**

These milestones are intentionally vertical-slice driven. They are not a commitment to implement every future capability; they are checkpoints for deciding whether the architecture has earned further investment.

## M0 — Execution substrate

**Status: DONE**

```text
Metadata
 → QueryIntent
 → QueryPlanner
 → QueryPlan
 → ProviderPlan
 → SQL
 → SQLite
 → ExecutionRow
```

Proven by the Banking and broader E2E suite.

---

## M1 — Semantic domain model

**Status: DONE**

Implemented in `Foundgine.Semantic`.

The active model covers:

```text
Entity
Identity
Field
Relationship
RelationshipCardinality
SearchCapability
```

Inference support can derive structural semantics from existing metadata. Explicit semantic configuration remains available where business meaning cannot safely be inferred.

Action/policy descriptors exist as future-facing primitives but are not part of the current proof target.

---

## M2 — Entity resolution

**Status: DONE**

Implemented:

```text
ResolveByIdentity
ResolveBySearch
ResolveByRelationship
```

with:

```text
Resolved
NotFound
Ambiguous
```

and evidence.

A real SQLite candidate source is exercised by the Banking proof.

### Important invariant

Resolution identifies **one concrete identity**. It must not silently collapse a collection-valued relationship into one arbitrary child.

---

## M3 — Structured read intent

**Status: PROVEN**

Implemented:

```text
ReadIntent
ReadPlanner
ResolvedReadPlan
```

The acceptance path proves the structured scenario:

> Find Ada Lovelace's last five transactions.

The intent is constructed as a structured object. Foundgine does not parse the English sentence.

---

## M4 — Semantic read bridge

**Status: NEXT**

Create a supported reusable translation from:

```text
ResolvedReadPlan
        ↓
QueryIntent
```

The current E2E tests prove that this translation works conceptually, but the translation is still assembled inside the acceptance path rather than exposed as a stable reusable runtime capability.

The bridge must preserve:

- resolved identity;
- traversal;
- filters;
- ordering;
- limits;
- ambiguity/failure state.

Do not create a second planner. `Foundgine.Planning.QueryPlanner` remains the logical planner.

---

## M5 — Collection-aware traversal and hard composite proof

**Status: NEXT**

The next semantic proof must handle one-to-many traversal without requiring every intermediate relationship to resolve to a single identity.

Target scenario:

```text
Customer
 ├── Account
 │    └── Transaction
 └── Account
      └── Transaction
```

Request:

> Find Ada's five most recent transactions across all her accounts.

Then repeat the proof on the deeper five-entity composite:

```text
Customer
 → CustomerBankingRelationship
 → Contract
 → Account
 → Transaction
```

and retain the repeated/self-joined entity proof.

---

## M6 — Benchmark and harden

**Status: NOT STARTED**

Measure:

```text
resolution
read planning
query planning
provider compilation
SQL translation
execution
total
```

across:

```text
single entity
linear
branching
five-entity composite
repeated entity
```

No performance conclusion should be published until the benchmark harness and environment are documented.

---

## M7 — Semantic mapping simplification

**Status: PLANNED**

Reduce duplicate configuration by deriving as much as possible from existing metadata:

```text
identity
fields
relationships
types
```

Semantic configuration should mainly express meaning that metadata cannot safely provide.

---

## M8 — Semantic actions

**Status: PLANNED**

Expose only explicit business operations:

```text
IssueRefund
SuspendAccount
ChangeTier
```

No arbitrary CLR method invocation.

---

## M9 — Policy and authorization

**Status: PLANNED**

Policy becomes part of the semantic execution path:

```text
Intent
 ↓
Resolve
 ↓
Authorize
 ↓
Plan
```

---

## M10 — Preview, execute, verify

**Status: PLANNED**

Mutations should eventually follow:

```text
Plan
 ↓
Preview
 ↓
Approve
 ↓
Execute
 ↓
Verify
 ↓
Evidence
```

---

## M11 — MCP

**Status: PLANNED**

Expose the semantic API through a thin MCP adapter.

MCP is not part of the core planning model.

---

## M12 — Additional execution targets

**Status: LATER**

Potential targets:

```text
Structured data
Semantic retrieval
Domain actions
External systems
```

Each should remain an execution adapter.

---

## M13 — Roslyn semantic compiler

**Status: LATER**

Generate application semantic descriptors where compile-time analysis can remove manual duplication.

Potential outputs:

- stable IDs;
- entity descriptors;
- relationship descriptors;
- search descriptors;
- action descriptors;
- policy metadata;
- planner hints.

Do not generate fixed plans for future natural-language requests.

---

# Release gate

Do not call the AI-facing runtime proven until a real application demonstrates:

```text
structured intent
 → resolution
 → collection-aware traversal
 → plan
 → real execution
 → verification/evidence
```

for a meaningful read and, later, a meaningful mutation scenario.

The first release gate is deliberately read-heavy. It should not be blocked by MCP, AOT, GraphQL or an LLM provider.


## M8 — Post-M7 Core Simplification

Completed: removed the unnecessary Foundgine.Metadata project dependency from Foundgine.Semantics.
