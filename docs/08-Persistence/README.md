[Home](../../README.md) → [Documentation](../README.md) → **Persistence**

# Persistence

Persistence is an execution target for Foundgine plans.

The current proof uses SQLite in the Banking sample because it is self-contained and requires no external database service.

The architecture is provider-oriented, but that does **not** mean every database provider is currently implemented.

## Current proof

```text
QueryPlan
 ↓
SqlPlanCompiler
 ↓
ProviderPlan
 ↓
SqlExecutionProvider
 ↓
SQLite
```

## Future

Other execution targets can be added without making them part of the core semantic model.

Potential integrations include:

- PostgreSQL
- SQL Server
- EF Core
- Dapper
- pgvector
- graph databases

These should remain implementation/integration choices rather than product identity.
