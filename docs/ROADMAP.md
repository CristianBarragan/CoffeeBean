# Roadmap

Foundgine 0.3.0 is the current shipped release. The core semantic execution pipeline is now validated by restore, build, and the full automated test suite. The roadmap therefore focuses on usefulness, provider depth, public API clarity, and evidence rather than another architecture-freeze cycle.

## M39 — Semantic authorization and capability discovery

M39 establishes granular authorization as part of semantic execution:

- entity read/write access;
- field read/write access;
- relationship read/write access;
- provider-independent conditional predicates;
- capability discovery for callers such as AI agents;
- mutation write authorization;
- authorization predicates preserved into the execution plan.

M39 deliberately does **not** introduce identity management, claims parsing,
role administration, OAuth/JWT handling, policy storage, or an authorization
server. Those concerns can sit above the semantic policy contract later.

The key invariant is:

```text
Caller capability context
        ↓
Authorization policy
        ↓
Semantic graph
        ↓
Authorization predicates
        ↓
Execution plan
        ↓
Provider execution
```

Capability discovery is advisory context only. Execution always evaluates the
configured policy again.

## M40 — Authorization-aware plan caching

M40 establishes a narrow, safe cache boundary for compiled provider plans.

- semantic resolution still runs on every request;
- authorization still runs on every request;
- only the provider compilation step is cached;
- authorization predicates remain in the cached provider plan;
- runtime execution context is resolved by the provider on every execution;
- exact request values are part of the current cache fingerprint.

This deliberately establishes correctness before introducing parameterized plan
templates or distributed caching.

## Near term

- simplify public APIs where the current contracts are more complex than necessary;
- improve provider composition and real-world examples;
- measure end-to-end performance;
- keep GraphQL and JSON adapters thin;
- document only capabilities that are implemented and tested.

## Later

Potential work includes more providers, richer semantic actions, claims/roles
integration above the policy contract, and stronger AI/agent integration.

These are ideas, not current core capabilities.

## Documentation rule

The active source and tests are the source of truth. Public documentation must distinguish implemented/demonstrated capabilities from planned work and historical material. See [Documentation truth](DOCUMENTATION-TRUTH.md).


## Execution IR

The canonical `ExecutionIR` boundary has been introduced. Provider migration to consume it directly is the next execution-layer step.

## M41 — Agent-safe execution contract

The agent execution surface is now defined as a single semantic lifecycle:

```text
Capability contract
        ↓
Intent
        ↓
Authorization
        ↓
Semantic plan
        ↓
Dry run
        ↓
Plan-bound approval
        ↓
Exact-plan execution
        ↓
Execution receipt
```

M41 includes semantic capability actions, dry-run inspection, plan-bound
approval, execution receipts, semantic version binding, and an MCP adapter that
translates MCP requests into the existing Foundgine semantic boundary.

MCP is a transport adapter, not an execution architecture.

## M42 — Policy-aware plan optimization

M42 introduces the first conservative policy-aware optimization pass:

- deterministic authorization predicate normalization;
- duplicate predicate elimination;
- commutative `AND`/`OR` canonicalization;
- double-negation elimination;
- deterministic plan fingerprints after normalization;
- improved compiled-plan cache reuse for semantically equivalent policies.

The optimizer must never grant authorization, evaluate context, or remove a
predicate merely because a transport or provider claims it is safe.

Future predicate placement and pushdown require explicit semantic proofs around
relationship cardinality, null behavior, aggregation, ordering, and pagination.

## M18.5 — Rewrite Cost Model + Rule Selection

- Introduce provider-neutral rewrite cost and benefit estimates.
- Select among currently applicable rewrite rules deterministically.
- Preserve ordering, conflicts, termination, semantic equivalence, and security proofs.
- Record rule-selection evidence for planner observability.

Next: provider-aware cost estimation and concrete optimization rules.

- M18.8 — Predicate Pushdown — implemented


## M18.13 — Aggregate Pushdown + Relationship Filter Interaction

Merge eligible COUNT-existence predicates with matching relationship `SOME` filters while preserving semantic and security proofs.

## M18.14 — Null / Empty / Cardinality Semantics

Centralize the empty-collection, NULL-input, and duplicate-sensitivity contract for COUNT/MIN/MAX in `SemanticAggregateSemanticsCatalog`, and add an `AggregateRewriteLegality` gate that rejects aggregate substitutions violating that contract (e.g. COUNT ↔ MIN). A semantic safety gate, not a new rewrite rule — it is the required foundation for M18.15.

Next: M18.15 — Aggregate Rewrite Safety. Safe `MIN`/`MAX`/`COUNT`/`SOME`/`NONE`/`ALL` predicate rewrites, gated on semantic equivalence, empty-set equivalence, NULL equivalence, duplicate equivalence, relationship cardinality proof, authorization preservation, provider capability, and cost evidence.

## M18.15 — Aggregate Rewrite Safety (Proof Gate)

Add `AggregateRewriteProof`, the composite fail-closed gate combining semantic equivalence, the M18.14 empty/NULL/duplicate/cardinality legality checks, provider capability, and the new `AuthorizationPreservationProof` security-regression check. This is the proof gate itself, not yet the rewrite rule that uses it.

Next: identify the first concrete, provably-correct aggregate/predicate rewrite (most likely `COUNT`-existence predicates collapsing into relationship quantifiers, continuing M18.13) and implement it as an `IPlanRewriteRule` gated by `AggregateRewriteProof`.
