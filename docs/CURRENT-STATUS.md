# Current status — Foundgine 0.3.0

Foundgine 0.3.0 is the current shipped release. The repository has crossed the build-validation gate: restore, compilation, and the full automated test suite have been run successfully for the current source tree.

## Proven by the active repository

- semantic entities, fields, relationships, and IDs;
- request resolution;
- read and write authorization;
- authorization rules carried into execution;
- provider-independent query planning;
- provider-independent mutation planning;
- SQL compilation;
- SQLite execution;
- a small InMemory provider;
- AOT metadata generation;
- JSON input;
- GraphQL input and mutations;
- nested relationships;
- filters and aggregates;
- cursor pagination;
- execution evidence and canonical receipts;
- deterministic plan fingerprints and provider-plan caching;
- MCP boundary and mutation-safe execution contracts;
- PostgreSQL integration contracts and PostgreSQL E2E workflows when PostgreSQL 17 is available.

## Validation gates

```text
dotnet restore   ✓
dotnet build     ✓
dotnet test      ✓

PostgreSQL E2E   separate environment-dependent gate
Benchmarks       separate measurement workflow
```

## Main rule

The semantic core does not depend on GraphQL or SQL.

```text
Input
 ↓
Semantics
 ↓
Authorization
 ↓
Plan
 ↓
Provider
 ↓
Result + Evidence
```

## PostgreSQL status

PostgreSQL 17 is part of the CI/integration path. Local PostgreSQL tests require:

```text
FOUNDGINE_POSTGRES_CONNECTION_STRING
```

PostgreSQL correctness and PostgreSQL performance are separate forms of evidence: integration tests establish runtime behavior, while the CoffeeBeanery benchmark documents measured performance for a specific workload.

## What is not claimed

The repository does not claim:

- universal database support;
- universal performance superiority;
- autonomous-agent execution as a general runtime;
- workflow orchestration;
- rollback or compensation semantics;
- production security or operational guarantees beyond the tested repository invariants.

## Source of truth

Use this order when information conflicts:

1. current source code;
2. active tests;
3. current documentation;
4. historical notes under `docs/history`.

Historical stage notes explain how the project changed. They are not the current design.
