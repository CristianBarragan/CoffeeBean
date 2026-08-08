[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Roadmap**

# Roadmap

> This roadmap predates the Phase 1 / future-phases framing introduced in
> [Architecture → Vision](../02-Architecture/Vision.md); the two describe the same trajectory
> at different levels of detail. Vision gives the four-item Phase 1 scope and four-item
> future-phases summary; this page gives the longer, phase-by-phase engineering breakdown.
> Where they'd conflict, Vision's Phase 1 boundary (EF Core mapping, Hot Chocolate,
> PostgreSQL, Dapper) is authoritative for "what's built today."

## Contents

- [Vision](#vision)
- [Phase 1 — Foundation](#phase-1--foundation)
- [Goals](#goals)
- [Phase 2 — Runtime](#phase-2--runtime)
- [Goals](#goals)
- [Phase 3 — SQL](#phase-3--sql)
- [Goals](#goals)
- [Phase 4 — Incremental Generator](#phase-4--incremental-generator)
- [Goals](#goals)
- [Phase 5 — Dependency Inversion](#phase-5--dependency-inversion)
- [Goals](#goals)
- [Phase 6 — GraphQL](#phase-6--graphql)
- [Goals](#goals)
- [Phase 7 — gRPC](#phase-7--grpc)
- [Goals](#goals)
- [Phase 8 — Web API](#phase-8--web-api)
- [Goals](#goals)
- [Phase 9 — Additional SQL Providers](#phase-9--additional-sql-providers)
- [Phase 10 — Graph Improvements](#phase-10--graph-improvements)
- [Phase 11 — Performance](#phase-11--performance)
- [Phase 12 — Native AOT](#phase-12--native-aot)
- [Phase 13 — Tooling](#phase-13--tooling)
- [Phase 14 — Ecosystem](#phase-14--ecosystem)
- [Success Criteria](#success-criteria)
- [Guiding Principles](#guiding-principles)
- [Summary](#summary)

---

> This document describes the long-term technical roadmap for the CoffeeBeanery framework. It outlines the expected evolution of the architecture while preserving the project's core design principles.

The roadmap is intentionally organized around architectural capabilities rather than release dates.

---

## Vision

CoffeeBeanery aims to become a compile-time-first data access framework capable of supporting multiple transports, multiple SQL dialects, and graph-based querying through a shared execution engine.

The long-term architecture is:

```
             Foundation
                  ▲
                  │
    ┌─────────────┼─────────────┐
    │             │             │
 Runtime         SQL      Source Generator
    ▲             ▲             │
    │             │             │
    └─────────────┼─────────────┘
                  │
       Generated Runtime Components
                  ▲
                  │
      ┌───────────┼───────────┐
      │           │           │
   GraphQL      gRPC       Web API
```

---

## Phase 1 — Foundation

## Goals

- Stable contracts
- Immutable metadata
- Planning primitives
- Runtime primitives
- Identifier system
- Dependency inversion

### Deliverables

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata
- ColumnReference
- QueryPlan
- MutationPlan
- IMetadataProvider
- IPlannerRegistry

This phase establishes the architectural foundation.

---

## Phase 2 — Runtime

## Goals

- Query execution
- Mutation execution
- Materialization pipeline
- Transaction coordination
- Dependency graph execution

### Deliverables

- QueryExecutor
- MutationExecutor
- ExecutionContext
- Runtime services
- Execution pipeline

Runtime should depend only on Foundation.

---

## Phase 3 — SQL

## Goals

- PostgreSQL support
- SQL writers
- SQL readers
- SQL dialect abstraction
- Apache AGE integration

### Deliverables

- PostgresSqlWriter
- PostgresSqlReader
- PostgreSqlDialect
- SQL builders
- SQL visitors

SQL serializes execution plans without performing planning.

---

## Phase 4 — Incremental Generator

## Goals

- Complete compile-time analysis
- Metadata generation
- Materializer generation
- Planner generation
- Runtime registrations

### Deliverables

- MetadataEmitter
- PlannerEmitter
- MaterializerEmitter
- DematerializerEmitter
- DependencyInjectionEmitter

Generated code should contain only precomputed information.

---

## Phase 5 — Dependency Inversion

## Goals

Replace static generated classes with generated implementations of Foundation contracts.

Instead of:

```
GeneratedMetadata.GetEntity(...)
```

Runtime should consume:

```csharp
IMetadataProvider
```

implemented by:

```csharp
GeneratedMetadataProvider
```

This decouples Runtime from generated code.

---

## Phase 6 — GraphQL

## Goals

- Schema generation
- Resolver generation
- Middleware
- Dependency Injection
- Transport adapter

GraphQL becomes a thin layer over Runtime.

---

## Phase 7 — gRPC

## Goals

- Protobuf integration
- Service generation
- Runtime adapter

The same Runtime should execute GraphQL and gRPC requests.

---

## Phase 8 — Web API

## Goals

- Controllers
- Minimal APIs
- OpenAPI integration
- Runtime adapter

Execution remains identical to GraphQL and gRPC.

---

## Phase 9 — Additional SQL Providers

Potential providers include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

The planner remains unchanged.

Only SQL serialization changes.

---

## Phase 10 — Graph Improvements

Future graph capabilities include:

- Recursive traversals
- Variable-length paths
- Path projections
- Graph aggregation
- Graph mutation optimization
- Graph query caching

These features extend planning while preserving Runtime behavior.

---

## Phase 11 — Performance

Areas of ongoing optimization:

- Reduced allocations
- Faster metadata lookup
- Improved SQL generation
- Streaming materialization
- Better cache locality
- Batch execution
- Object pooling where appropriate

Performance improvements should remain architecture-driven.

---

## Phase 12 — Native AOT

Continue validating:

- Metadata provider
- Materializers
- Planner registry
- SQL generation
- Runtime execution

No feature should compromise Native AOT compatibility.

---

## Phase 13 — Tooling

Developer tooling may include:

- Visual Studio integration
- Roslyn analyzers
- Diagnostic visualizers
- SQL preview
- Query plan visualizer
- Graph explorer

These tools should consume generated metadata where possible.

---

## Phase 14 — Ecosystem

Potential ecosystem projects:

```
CoffeeBeanery.Mongo

CoffeeBeanery.Redis

CoffeeBeanery.Cosmos

CoffeeBeanery.Elasticsearch

CoffeeBeanery.Blazor

CoffeeBeanery.OpenApi

CoffeeBeanery.Cli
```

Each project integrates through Foundation contracts.

---

## Success Criteria

CoffeeBeanery will be considered architecturally complete when:

- Runtime contains no reflection.
- Generated code implements all required contracts.
- GraphQL, gRPC, and Web API share the same Runtime.
- SQL dialects are interchangeable.
- Native AOT is fully supported.
- Metadata is immutable.
- Execution plans are immutable.
- Generated artifacts are deterministic.

---

## Guiding Principles

Future development should preserve the following principles:

- Compile-time over runtime
- Immutable metadata
- Immutable execution plans
- Dependency inversion
- Transport independence
- Database abstraction
- Deterministic generation
- Clear project boundaries
- Single responsibility

Architectural consistency is more valuable than short-term convenience.

---

## Summary

The CoffeeBeanery roadmap is focused on evolving the framework through compile-time generation, transport independence, and dependency inversion.

Rather than adding isolated features, each phase strengthens the overall architecture, ensuring the framework remains performant, maintainable, extensible, and capable of supporting new transports and storage providers without compromising its core design.

---

## Related Documentation

- [Architecture → Vision](../02-Architecture/Vision.md)
- [Changelog](Changelog.md)
- [ADRs](ADRs.md)

---

← Previous: [Glossary](Glossary.md)  |  Next: [Changelog](Changelog.md) →
