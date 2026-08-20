# Documentation

These documents describe the current Foundgine 0.4.0 release. Historical phase and milestone material is kept under `docs/history`.

Use these pages in this order.

## Start here

1. [Getting started](GETTING-STARTED.md)
2. [Layer setup](LAYER-SETUP.md)
3. [Architecture](ARCHITECTURE.md)
4. [Testing](TESTING.md)
5. [PostgreSQL E2E](POSTGRES-E2E.md)
6. [Current status](CURRENT-STATUS.md)
7. [Release 0.4.0](RELEASE-0.4.0.md)

## Understand the product

- [Product identity](PRODUCT-IDENTITY.md)
- [Why Foundgine](WHY-FOUNDGINE.md)
- [Provider independence](PROVIDER-INDEPENDENCE.md)
- [Security](SECURITY.md)
- [AI capability interface](AI-CAPABILITY-INTERFACE.md)
- [Agent semantic boundary](AGENT-SEMANTIC-BOUNDARY.md)

## Work on the implementation

- [Architecture boundaries](ARCHITECTURE-BOUNDARIES.md)
- [Semantic IR](SEMANTIC-IR.md)
- [Execution IR](EXECUTION-IR.md)
- [Execution algebra](EXECUTION-ALGEBRA.md)
- [Semantic mutation plan](SEMANTIC-MUTATION-PLAN.md)
- [Semantic mutation planner](SEMANTIC-MUTATION-PLANNER.md)
- [Authorization](AUTHORIZATION.md)
- [Execution evidence](EVIDENCE.md)
- [AOT](AOT.md)
- [GraphQL](GRAPHQL.md)
- [Runtime](RUNTIME.md)

## PostgreSQL

- [PostgreSQL physical boundary](POSTGRES-PHYSICAL-BOUNDARY.md)
- [PostgreSQL correlation](POSTGRES-BATCH-CORRELATION-INVARIANTS.md)
- [PostgreSQL generated-key correlation](POSTGRES-GENERATED-KEY-CORRELATION.md)
- [PostgreSQL E2E measurement gate](stage-48-MEASUREMENT-GATE-RECOMMENDATION.md)

## History

Historical design notes and old stage material are under [history](history/README.md).

- [Release 0.3.0](RELEASE-0.3.0.md)

When current code and historical notes disagree, use the code and current tests as the source of truth.

- [M17.2 — Model-Provider Replay](MILESTONE-M17.2-MODEL-PROVIDER-REPLAY.md)
- [M17.3 — Security Invariant Registry](MILESTONE-M17.3-SECURITY-INVARIANT-REGISTRY.md)
- [M17.4 — Plan-Level Security Invariant Proof](MILESTONE-M17.4-PLAN-LEVEL-INVARIANT-PROOF.md)
- [M17.5 — SQL Provider Security Conformance](MILESTONE-M17.5-SQL-PROVIDER-CONFORMANCE.md)
- [M17.6 — High-Assurance Mutation Conformance](MILESTONE-M17.6-HIGH-ASSURANCE-MUTATION-CONFORMANCE.md)
- [M17.7 — Cross-Provider Security Conformance](MILESTONE-M17.7-CROSS-PROVIDER-SECURITY-CONFORMANCE.md)

- M18.1/M18.2 — Security-Preserving Plan Rewriting + Semantic Equivalence: `MILESTONE-M18.1-SECURITY-PRESERVING-PLAN-REWRITING.md`
- M18.3 — Rewrite Rule Contracts: `MILESTONE-M18.3-REWRITE-RULE-CONTRACTS.md`

- [M18.4 — Rewrite Rule Algebra + Composition](MILESTONE-M18.4-REWRITE-RULE-ALGEBRA.md)
- [M18.5 — Rewrite Cost Model + Rule Selection](MILESTONE-M18.5-REWRITE-COST-MODEL-RULE-SELECTION.md)
- [M18.6 — Provider-Aware Cost Estimation](MILESTONE-M18.6-PROVIDER-AWARE-COST-ESTIMATION.md)

- [M18.7 — Cost Provenance + Statistics Freshness](MILESTONE-M18.7-COST-PROVENANCE-STATISTICS-FRESHNESS.md)

- [M18.8 — Predicate Pushdown](MILESTONE-M18.8-PREDICATE-PUSHDOWN.md)
- [M18.9 — Projection Pruning](MILESTONE-M18.9-PROJECTION-PRUNING.md)
- [M18.10 — Relationship Traversal Optimization](MILESTONE-M18.10-RELATIONSHIP-TRAVERSAL-OPTIMIZATION.md)
- [M18.11 — Join Ordering / Multi-Relationship Planning](MILESTONE-M18.11-JOIN-ORDERING-MULTI-RELATIONSHIP-PLANNING.md)

- [M18.12 — Aggregate / Cardinality-Aware Optimization](MILESTONE-M18.12-AGGREGATE-CARDINALITY-OPTIMIZATION.md)

- [M18.13 — Aggregate Pushdown + Relationship Filter Interaction](MILESTONE-M18.13-AGGREGATE-RELATIONSHIP-FILTER-PUSHDOWN.md)
- [M18.14 — Null / Empty / Cardinality Semantics](MILESTONE-M18.14-NULL-EMPTY-CARDINALITY-SEMANTICS.md)
