# Current status

Foundgine has a working end-to-end foundation.

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
3. current documentation;
4. historical notes under `docs/history`.

Historical stage notes explain how the project changed. They are not the current design.
