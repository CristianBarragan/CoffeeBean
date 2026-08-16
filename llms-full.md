# Foundgine — AI context

Foundgine is a semantic execution layer for .NET.

It separates:

```text
what the caller wants
```

from:

```text
how a provider executes it
```

## Core flow

```text
Request
 ↓
Resolve
 ↓
Authorize
 ↓
Plan
 ↓
Provider
 ↓
Result
```

## Vocabulary

**Model**: what the application exposes.

**Request**: what the caller wants.

**Authorization**: what the caller may do.

**Plan**: a provider-independent description of the work.

**Provider**: the physical executor.

**Result**: returned data and execution evidence.

## Architecture

Input adapters include JSON and GraphQL. AI and application code can also create structured requests.

The semantic core owns meaning, resolution, authorization, planning, execution contracts, and result handling.

Providers own physical execution.

The core must not depend on a transport or provider.

## Current projects

`Foundgine.Abstractions` contains stable IDs and small contracts.

`Foundgine.Metadata` describes application and storage metadata.

`Foundgine.Semantics` owns semantic meaning, request resolution, authorization, and capability discovery.

`Foundgine.Planning` creates provider-independent plans.

`Foundgine.Execution` defines provider execution contracts and result materialization.

`Foundgine.Sql` is the SQL provider.

`Foundgine.InMemory` is a deliberately small non-SQL provider used to test provider independence.

`Foundgine.Intent.Json` and `Foundgine.GraphQL.HotChocolate*` are input adapters.

`Foundgine.Aot` and `Foundgine.Aot.Generator` provide compile-time metadata.

## AI boundary

AI is an input source, not the authority.

An AI system can ask for:

```text
Customer
 ├── name
 └── orders
```

Foundgine decides whether that request is valid and authorized, then builds the plan.

Capability discovery is descriptive. It does not grant permission.

## Current proof

The active tests cover semantic modelling, resolution, authorization, provider-independent query and mutation planning, SQL/SQLite execution, a small InMemory provider, AOT metadata, JSON input, GraphQL adapters, relationships, aggregates, pagination, and PostgreSQL integration.

Historical material is under `docs/history`.

When documentation and code disagree, current code and active tests win.

## M17.5 — SQL Provider Security Conformance

M17.5 adds provider-specific structural conformance for compiled SQL plans. Required security invariants are checked against concrete SQL-plan evidence: authorization predicates, runtime authorization parameterization, parameter bindings, explicit field projections, relationship execution shape, and plan-cache context isolation. Mutation guarantees such as atomicity and idempotency remain the responsibility of the high-assurance mutation provider contract and are not inferred from ordinary query SQL.


## M17.7 — Cross-Provider Security Conformance

M17.7 makes provider security differences explicit through a provider-neutral conformance matrix. Providers declare the security invariants they can preserve; a capability can execute only when its required invariants are a subset of the selected provider's preserved invariants. Generic SQL is deliberately not allowed to claim high-assurance mutation guarantees, while the PostgreSQL TransferFunds provider carries the stronger atomicity, idempotency, replay-protection, audit, and execution-evidence contract established by M16.5/M16.6. Unknown providers and unknown invariants fail closed.

## M18.6 — Provider-Aware Cost Estimation

Foundgine supports an optional provider-aware rewrite cost boundary through `IProviderCostEstimator`. Providers can estimate execution cost for candidate semantic plans and influence deterministic rewrite selection. Provider cost is advisory and cannot bypass semantic-equivalence or security-preservation proofs. The first implementation is `Foundgine.Sql.SqlCostEstimator`, a conservative heuristic model based on semantic plan shape, projections, traversals, filters, ordering, pagination, and child nodes. It is not presented as a replacement for database-native statistics or query optimization.

## M18.9 — Projection Pruning

Foundgine now includes the conservative `projection.pruning` rewrite rule. It removes redundant duplicate projection fields while preserving field order and all unique requested fields. `ProjectionPruningRequirements` identifies fields required by output projection, root filters, and root ordering.

The current semantic plan does not distinguish requested output fields from internal working fields, so M18.9 deliberately does not remove unique fields. Full dead-field pruning requires an explicit requested-vs-working projection representation.

The rewrite remains subject to semantic-equivalence and security-preservation proofs and participates in the existing provider-aware cost-selection framework.


## M18.11 — Join Ordering / Multi-Relationship Planning

Foundgine can assign deterministic `TraversalOrder` metadata to sibling relationship traversals when cardinality is known. The rule uses conservative selectivity signals and never changes logical child order, authorization, filters, pagination, or relationship identity. `TraversalOrder` is physical metadata: excluded from semantic equivalence and included in execution plan fingerprints. Providers may use it for safe physical traversal planning.


## M18.13 Aggregate Relationship Filter Pushdown

The planner recognizes COUNT-existence plus SOME relationship predicates and can represent the equivalent filtered COUNT. The transformation is bounded to proven count-existence cases and remains subject to semantic-equivalence, security-preservation, provider-capability, and cost checks.


## End-to-end agent benchmark

The repository contains an end-to-end benchmark comparing a conventional application/AI tool flow with a Foundgine semantic flow against the same PostgreSQL fixture. It measures provider-reported input/output/total tokens, cached input tokens, model calls, tool calls, wall-clock time, model time, tool time and final-state equivalence. See `benchmarks/AgentEndToEnd` and the website page `/agent-benchmark/`.
