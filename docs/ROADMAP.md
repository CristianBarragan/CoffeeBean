# Roadmap

Foundgine 0.4.0 is the current shipped release. The core semantic execution pipeline is now validated by restore, build, and the full automated test suite. As of 0.4.0 there are no pending/in-flight milestones below — the M18.x plan-rewrite series and the M39–M42 agent/authorization/caching work described in this document are all implemented and shipped (see [docs/README.md](README.md) for the milestone index and [RELEASE-0.4.0.md](RELEASE-0.4.0.md) for the release surface). The sections below are kept for design-rationale context; the "Near term" and "Later" sections at the end are the actual open/forward-looking items.

## M39 — Semantic authorization and capability discovery (implemented)

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

## M40 — Authorization-aware plan caching (implemented)

See [M40 — Plan caching](M40-PLAN-CACHING.md) and [Context-safe plan caching](CONTEXT-SAFE-PLAN-CACHING.md) for the shipped design.

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


## Execution IR (implemented)

The canonical `ExecutionIR` boundary has been introduced. Both the SQL and InMemory providers now consume it directly.

## M41 — Agent-safe execution contract (implemented)

See [MCP adapter](MCP-ADAPTER.md), [Plan approval](PLAN-APPROVAL.md), [Execution receipts](EXECUTION-RECEIPTS.md), and [Dry run and plan inspection](DRY-RUN-AND-PLAN-INSPECTION.md) for the shipped design.

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

## M42 — Policy-aware plan optimization (implemented)

See [Policy-aware planning](POLICY-AWARE-PLANNING.md) for the shipped design; `AuthorizationCanonicalizationRule` in `src/Foundgine.Planning` is the canonicalization pass described below.

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

## M18.5 — Rewrite Cost Model + Rule Selection (implemented)

- Introduce provider-neutral rewrite cost and benefit estimates.
- Select among currently applicable rewrite rules deterministically.
- Preserve ordering, conflicts, termination, semantic equivalence, and security proofs.
- Record rule-selection evidence for planner observability.

Provider-aware cost estimation (M18.6) and the concrete optimization rules below (M18.8–M18.15) are implemented; see the `docs/MILESTONE-M18.*.md` notes for each rule's contract.

- M18.8 — Predicate Pushdown — implemented


## M18.13 — Aggregate Pushdown + Relationship Filter Interaction (implemented)

Merge eligible COUNT-existence predicates with matching relationship `SOME` filters while preserving semantic and security proofs. Shipped as `AggregateRelationshipFilterPushdownRule` in `src/Foundgine.Planning`.

## M18.14 — Null / Empty / Cardinality Semantics (implemented)

Centralize the empty-collection, NULL-input, and duplicate-sensitivity contract for COUNT/MIN/MAX in `SemanticAggregateSemanticsCatalog`, and add an `AggregateRewriteLegality` gate that rejects aggregate substitutions violating that contract (e.g. COUNT ↔ MIN). A semantic safety gate, not a rewrite rule itself — it is the foundation M18.15 builds on.

## M18.15 — Aggregate Rewrite Safety (Proof Gate) (implemented)

Adds `AggregateRewriteProof`, the composite fail-closed gate combining semantic equivalence, the M18.14 empty/NULL/duplicate/cardinality legality checks, provider capability, and `AuthorizationPreservationProof` security-regression checks. `AggregateExistenceCollapseRule` (COUNT-existence predicates collapsing into relationship quantifiers) is the concrete rewrite rule gated by this proof, shipped alongside it in `src/Foundgine.Planning`.
