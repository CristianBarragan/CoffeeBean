# Current status — Foundgine 0.3.0

Foundgine 0.3.0 is the current shipped release. The repository contains the semantic execution pipeline, authorization-aware planning, SQL and InMemory execution paths, GraphQL/JSON adapters, AOT metadata support, AI integration surfaces, execution evidence, and PostgreSQL integration infrastructure.

## Proven by the active code and tests

- semantic entities, fields, relationships, and IDs;
- request resolution;
- read and write authorization;
- authorization rules carried into execution;
- provider-independent query planning;
- provider-independent mutation planning;
- SQL compilation;
- SQLite execution;
- InMemory execution;
- AOT metadata generation;
- JSON input;
- GraphQL input and mutations;
- nested relationships;
- filters and aggregates;
- cursor pagination;
- execution evidence;
- PostgreSQL integration contracts;
- PostgreSQL E2E tests when PostgreSQL is configured.

## Validation status

The latest supplied test run is **not green**. It reported failures in semantic security/capability validation and an aggregate optimizer expectation, while the JSON intent safety suite passed. PostgreSQL integration tests also require a configured PostgreSQL connection and should be treated as environment-dependent integration tests rather than silently interpreted as unit-test failures.

This document intentionally does not claim a passing full-suite result until a fresh `dotnet test Foundgine.sln` run is green.

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

PostgreSQL 17 is part of the PR test path. The local PostgreSQL tests use `FOUNDGINE_POSTGRES_CONNECTION_STRING`. CI starts its own PostgreSQL 17 container for database-backed checks.

## What is not claimed

The repository does not claim:

- universal database support;
- universal performance superiority;
- autonomous agent execution;
- workflow orchestration;
- authentication or identity management;
- automatic correctness of business policy;
- rollback or compensation semantics for every provider.

## Source of truth

Use this order when information conflicts:

1. current source code;
2. active tests;
3. current documentation;
4. historical notes under `docs/history`.

Historical milestone documents explain how the project changed. They are not the current design or a priority list.
