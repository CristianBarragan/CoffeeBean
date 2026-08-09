# Roadmap

[Home](../../README.md) → [Reference](README.md) → **Roadmap**

The roadmap is intentionally vertical-slice driven. Foundgine should prove the core thesis before expanding into multiple transports, databases, retrieval systems, or AI integrations.

## Phase 0 — Execution substrate

**Status: proven in the canonical sample**

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ SQLite
→ Result
```

The Banking sample is the acceptance test.

## Phase 1 — Semantic domain

**Status: next**

Create a protocol-neutral semantic model representing:

- entities
- identities
- fields
- relationships
- searchable properties
- actions
- policies

Start hand-authored if necessary.

The goal is to prove the model before building a compiler.

## Phase 2 — Resolution

**Status: planned immediately after Phase 1**

Resolve human/agent references into domain identities.

Required behavior:

- exact matches
- useful search fields
- relationship traversal
- ambiguity reporting
- evidence for why an entity was selected

No silent guessing.

## Phase 3 — Read planning

**Status: planned**

Convert an intent into a Foundgine query plan.

```text
Intent
→ Resolve
→ Semantic query
→ QueryPlan
→ ProviderPlan
→ Execute
→ Evidence
```

The first test is the Banking "last five transactions" scenario.

## Phase 4 — Domain actions

**Status: planned**

Introduce explicit, constrained action descriptors.

```text
IssueRefund
SuspendAccount
ChangeTier
```

Agents may select declared actions; they cannot invoke arbitrary CLR methods.

## Phase 5 — Policy and authorization

**Status: planned**

Make authorization a planning input rather than a late controller concern.

```text
Intent
→ Resolve
→ Policy
→ Plan
```

The result should explain allow/deny decisions.

## Phase 6 — Preview and approval

**Status: planned**

Mutations become:

```text
Plan
→ Preview
→ Approve
→ Execute
```

Preview is part of the execution contract.

## Phase 7 — Verification and evidence

**Status: planned**

Every important mutation should verify expected state after execution and produce an evidence chain.

## Phase 8 — MCP

**Status: planned**

MCP becomes a thin adapter over the semantic API.

Initial surface:

```text
discover
resolve
plan/query
preview
execute
evidence
```

Do not create an entity-specific tool for every domain operation unless a concrete integration proves it necessary.

## Phase 9 — More execution targets

**Status: later**

Add execution targets behind the existing plan:

```text
Structured data
Domain actions
Semantic retrieval
External data
```

Do not build all targets in parallel.

## Phase 10 — Compile-time semantic compiler

**Status: later**

Use Roslyn to derive and generate:

- stable IDs
- entity metadata
- relationship metadata
- search descriptors
- action descriptors
- policy metadata
- planner hints

The compiler describes the legal application vocabulary.

It does not attempt to generate future natural-language plans.

## Phase 11 — Ecosystem integrations

Potential integrations:

- ASP.NET Core
- MCP
- Semantic Kernel
- OpenTelemetry
- EF Core
- Dapper
- Temporal
- Kafka
- PostgreSQL/pgvector
- other databases and retrieval systems

These remain adapters/integrations rather than the Foundgine core.

## Explicit non-goals

The roadmap does not include building:

- a proprietary LLM
- a general agent framework
- a proprietary vector database
- a replacement for MCP
- a replacement for EF Core
- a replacement for Temporal
- a replacement for Kafka

## The release gate

Do not call the first AI-native milestone complete until a real test demonstrates:

```text
natural-language request
→ domain resolution
→ policy
→ plan
→ real execution
→ verification
→ evidence
```

for both a read and a mutation.
