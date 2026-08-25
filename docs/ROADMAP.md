# Roadmap

Foundgine 1.0.0 is the current shipped release. The core semantic execution pipeline is validated by restore, build, and the full automated test suite. There are no pending/in-flight milestones below — the work described in this document is implemented and shipped (see [docs/README.md](README.md) for the documentation index and [RELEASE-1.0.0.md](RELEASE-1.0.0.md) for the release surface, which carries forward [RELEASE-0.5.0.md](RELEASE-0.5.0.md) unchanged). The sections below are kept for design-rationale context; the "Near term" and "Later" sections at the end are the actual open/forward-looking items.

## Semantic authorization and capability discovery (implemented)

Granular authorization as part of semantic execution:

- entity read/write access;
- field read/write access;
- relationship read/write access;
- provider-independent conditional predicates;
- capability discovery for callers such as AI agents;
- mutation write authorization;
- authorization predicates preserved into the execution plan.

This deliberately does **not** introduce identity management, claims parsing,
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

## Authorization-aware plan caching (implemented)

A narrow, safe cache boundary for compiled provider plans:

- semantic resolution still runs on every request;
- authorization still runs on every request;
- only the provider compilation step is cached;
- authorization predicates remain in the cached provider plan;
- runtime execution context is resolved by the provider on every execution;
- exact request values are part of the current cache fingerprint.

This deliberately establishes correctness before introducing parameterized plan
templates or distributed caching.

## Execution IR (implemented)

The canonical `ExecutionIR` boundary has been introduced. Both the SQL and InMemory providers now consume it directly.

## Agent-safe execution contract (implemented)

The agent execution surface is defined as a single semantic lifecycle:

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

This includes semantic capability actions, dry-run inspection, plan-bound
approval, execution receipts, semantic version binding, and an MCP adapter that
translates MCP requests into the existing Foundgine semantic boundary.

MCP is a transport adapter, not an execution architecture.

## Policy-aware plan optimization (implemented)

The first conservative policy-aware optimization pass, implemented as `AuthorizationCanonicalizationRule` in `src/Foundgine.Planning`:

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

## Plan-rewrite optimizer suite (implemented)

Provider-neutral rewrite cost and benefit estimation, deterministic rewrite-rule selection, and the concrete optimization rules below are all implemented in `src/Foundgine.Planning`. Every accepted rewrite preserves ordering, termination, semantic equivalence, and security proofs.

- **Predicate pushdown** — implemented.
- **Projection pruning** — a conservative rule that removes redundant duplicate fields without changing requested field order. Fields required by filters and ordering are tracked explicitly. The current semantic model intentionally does not remove unique requested fields, because output and working projections are not yet represented separately — that stronger dead-field optimization is reserved for future work.
- **Relationship traversal / join ordering** — conservative cardinality- and selectivity-aware traversal ordering metadata for sibling relationship plans. Logical child order remains unchanged; providers may use `TraversalOrder` for physical planning subject to semantic and security conformance.
- **Aggregate pushdown + relationship filter interaction** — merges eligible COUNT-existence predicates with matching relationship `SOME` filters while preserving semantic and security proofs. Shipped as `AggregateRelationshipFilterPushdownRule`.
- **Null / empty / cardinality semantics** — centralizes the empty-collection, NULL-input, and duplicate-sensitivity contract for COUNT/MIN/MAX in `SemanticAggregateSemanticsCatalog`, with an `AggregateRewriteLegality` gate that rejects aggregate substitutions violating that contract (e.g. COUNT ↔ MIN). A semantic safety gate, not a rewrite rule itself — it is the foundation the aggregate rewrite safety gate below builds on.
- **Aggregate rewrite safety (proof gate)** — `AggregateRewriteProof`, the composite fail-closed gate combining semantic equivalence, the null/empty/duplicate/cardinality legality checks, provider capability, and `AuthorizationPreservationProof` security-regression checks. `AggregateExistenceCollapseRule` (COUNT-existence predicates collapsing into relationship quantifiers) is the concrete rewrite rule gated by this proof.

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

The active source and tests are the source of truth. Public documentation must distinguish implemented/demonstrated capabilities from planned work and historical material.
