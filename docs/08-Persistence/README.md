[Home](../../README.md) → [Documentation](../README.md) → **Persistence**

# Persistence

Persistence is where a generated execution plan meets an actual database. Phase 1 ships one
execution provider — PostgreSQL, with Apache AGE for graph-shaped reads — but the SQL layer
is deliberately structured as a **provider**, not baked into the runtime, so
[future phases](../02-Architecture/Vision.md#roadmap-by-phase) can add SQL Server, MySQL, or
others without the planner changing. See [Architecture → Vision](../02-Architecture/Vision.md).

---

## Contents

- [PostgreSQL & AGE](PostgreSQL-AGE.md) — the Phase 1 execution provider and the graph read path
- [Dapper & EF Core](Dapper-EFCore.md) — how the two coexist (metadata vs. execution)
- [Caching](Caching.md) — the warmup pipeline and in-process caching

---

## Philosophy

## Philosophy

The SQL layer has one responsibility:

> Convert execution plans into SQL.

It should never:

- Discover metadata
- Analyze CLR models
- Parse GraphQL
- Resolve relationships
- Perform planning

Planning belongs to the Runtime and Generator.

---

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [Dependency Injection](../07-Dependency-Injection/README.md)  |  Next: [AI & LLM Readiness](../09-AI/README.md) →
