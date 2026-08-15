# Documentation truth

Foundgine documentation uses five claim states. This prevents historical plans and architectural intent from being mistaken for implemented capability.

| State | Meaning |
|---|---|
| **Implemented** | Present in the active source tree. |
| **Demonstrated** | Implemented and covered by an active test or explicit repository proof. |
| **Planned** | Intentionally proposed future work; not a current capability. |
| **Released** | Implemented, tested, and included in the current shipped release. |
| **Historical** | Retained under `docs/history` or `archive` to explain prior direction; not current product identity. |

## Current release

**0.2.1** is the current shipped release. The release has passed the repository restore, build, and full automated test gates.

## Current demonstrated surface

The active repository demonstrates:

- semantic modelling and request resolution;
- granular authorization, including conditional predicates carried into execution plans;
- provider-independent query and mutation planning;
- SQL compilation and SQLite execution;
- a deliberately small CLR-backed in-memory provider consuming the same logical execution plan for its tested subset;
- nested traversal, filtering, ordering, aggregation, and pagination capabilities covered by tests;
- deterministic execution-plan fingerprints and provider-plan caching;
- execution evidence with intent and authorization fingerprints;
- AOT metadata generation;
- structured JSON intent; and
- Hot Chocolate GraphQL query and mutation adapters;
- MCP boundary and mutation-safe execution contracts; and
- canonical execution receipts and plan-bound execution evidence.

## Current non-claims

The repository does not establish that Foundgine is:

- an autonomous-agent runtime;
- a workflow/orchestration engine;
- a universal provider abstraction with feature parity across providers;
- an ORM replacement; or
- a benchmark winner.

## Benchmark evidence

The active `benchmarks/` tree contains benchmark harnesses and reports for specific environments and runs. These documents should be treated as measured evidence for those runs only. They must not be converted into broad performance claims without a documented benchmark methodology and comparable results.

## Historical material

`docs/history/` and `archive/` preserve earlier architectural directions, Graphgine material, CoffeeBeanery prototypes, and other historical work. They are useful context but are not part of the current product identity or runtime architecture.

## Editing rule

If documentation conflicts with active source and tests, update the documentation. Do not make a stronger claim merely because it appears in an older milestone document.
