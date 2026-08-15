# Foundgine 0.3.0

Foundgine 0.3.0 is the current shipped release. It represents the point at which the semantic execution architecture is backed by a real repository validation cycle rather than static architectural inspection alone.

## Release gates

The current source tree has passed:

```text
dotnet restore   ✓
dotnet build     ✓
dotnet test      ✓
```

The repository also contains separate PostgreSQL integration and CoffeeBeanery benchmark workflows. Those require their own database/runtime environment and should be reported separately from the normal solution test gate.

## Core release surface

- semantic modeling and request resolution;
- authorization-aware query and mutation planning;
- provider-independent execution planning;
- SQL and InMemory execution paths;
- JSON and Hot Chocolate GraphQL adapters;
- AOT metadata generation;
- MCP boundary and mutation-safe execution contracts;
- execution receipts and plan-bound approval;
- deterministic plan fingerprints and provider-plan caching;
- relationship traversal, filtering, aggregation, and pagination; and
- PostgreSQL integration contracts and E2E/benchmark workflows.

## Deliberate non-claims

0.3.0 does not establish Foundgine as:

- an autonomous-agent runtime;
- a workflow/orchestration engine;
- a universal provider abstraction with feature parity across all providers;
- an ORM replacement;
- an identity or authorization provider; or
- a universally faster alternative to EF Core, GraphQL servers, or other execution stacks.

## Evidence policy

Behavioral claims should be backed by active tests. Performance claims should identify the workload, fixture, concurrency, provider versions, and measurement method. Historical phase documents explain prior design decisions but are not release specifications.

## Historical reconciliation

The Phase 13/14 audit and build-gate documents are retained under `docs/history/reconciliation`. They record the pre-validation state and should not be read as the current build status.
