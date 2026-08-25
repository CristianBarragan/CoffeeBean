# Current status — Foundgine 1.0.0

Foundgine 1.0.0 is the current shipped release. It is a semver-stability milestone: no `src/` package changed since 0.5.0, so the restore/compilation/test evidence below — run against that unchanged source — still applies. See [RELEASE-1.0.0.md](RELEASE-1.0.0.md) for what did change in this release (outside `src/`).

## Proven by the active tests

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
- execution evidence;
- PostgreSQL integration contracts;
- real PostgreSQL E2E tests when PostgreSQL 17 is available.

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
Result
```

## PostgreSQL status

PostgreSQL 17 is part of the PR test path.

The local PostgreSQL tests require:

```text
FOUNDGINE_POSTGRES_CONNECTION_STRING
```

The CI job starts its own PostgreSQL 17 container, so the database tests are real PR checks.

## What is not claimed

The repository does not claim:

- universal database support;
- universal performance superiority;
- autonomous agent execution;
- workflow orchestration;
- rollback or compensation semantics.

Those claims require separate implementation and evidence.

## Source of truth

Use this order when information conflicts:

1. current source code;
2. active tests;
3. current documentation.
