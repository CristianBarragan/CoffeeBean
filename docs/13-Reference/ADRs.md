[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **ADRs**

# Architecture Decision Records

Twelve foundational ADRs, recorded when the compile-time-first architecture was adopted.
New ADRs are appended here — see [Contributing → ADR Process](../12-Contributing/ADR-Process.md)
before proposing one.

## Contents

- [ADR-001 — Compile-Time First Architecture](#adr-001-compile-time-first-architecture)
- [ADR-002 — Foundation Owns Contracts](#adr-002-foundation-owns-contracts)
- [ADR-003 — Runtime Depends on Interfaces](#adr-003-runtime-depends-on-interfaces)
- [ADR-004 — Immutable Metadata](#adr-004-immutable-metadata)
- [ADR-005 — Immutable Execution Plans](#adr-005-immutable-execution-plans)
- [ADR-006 — Generated Materializers](#adr-006-generated-materializers)
- [ADR-007 — SQL Is a Serialization Layer](#adr-007-sql-is-a-serialization-layer)
- [ADR-008 — GraphQL Is a Transport](#adr-008-graphql-is-a-transport)
- [ADR-009 — Dependency Inversion for Generated Code](#adr-009-dependency-inversion-for-generated-code)
- [ADR-010 — Stable Identifier Allocation](#adr-010-stable-identifier-allocation)
- [ADR-011 — Transport Independence](#adr-011-transport-independence)
- [ADR-012 — Native AOT Compatibility](#adr-012-native-aot-compatibility)
- [Summary](#summary)

---

> This document records the major architectural decisions that shape the CoffeeBeanery framework. It is intended to provide context for contributors and future maintainers, explaining not only **what** the architecture is, but **why** specific design choices were made.

---

## ADR-001 — Compile-Time First Architecture

### Status

Accepted

### Context

Traditional GraphQL frameworks perform extensive runtime analysis using reflection, expression trees, and dynamic code generation. This increases startup time, memory usage, and complexity while limiting compatibility with Native AOT.

### Decision

CoffeeBeanery moves as much work as possible from runtime to compile time using Roslyn Incremental Source Generators.

Compilation is responsible for:

- Metadata discovery
- Relationship analysis
- Identifier allocation
- Materializer generation
- Dematerializer generation
- Planner registry generation
- Runtime registrations

Runtime executes precomputed artifacts.

### Consequences

### Advantages

- Faster startup
- Native AOT compatibility
- Reduced allocations
- Deterministic execution
- Simpler runtime

### Trade-offs

- More complex generator
- Larger generated code
- Increased compile-time work

---

## ADR-002 — Foundation Owns Contracts

### Status

Accepted

### Context

Runtime, SQL, GraphQL, and generated code require a common vocabulary.

Without a dedicated foundation layer, dependencies become cyclic and implementations leak across project boundaries.

### Decision

Foundation defines:

- Metadata
- Planning primitives
- Interfaces
- Runtime primitives
- Identifiers

Foundation references no other CoffeeBeanery project.

### Consequences

Every project shares the same contracts while remaining loosely coupled.

---

## ADR-003 — Runtime Depends on Interfaces

### Status

Accepted

### Context

The original Runtime directly referenced generated static classes such as:

```csharp
GeneratedMetadata.GetEntity(...)
```

This tightly coupled Runtime to generated code.

### Decision

Runtime depends on abstractions instead.

Example:

```csharp
IMetadataProvider
```

implemented by:

```csharp
GeneratedMetadataProvider
```

### Consequences

Generated code becomes a plug-in rather than a dependency.

Runtime becomes reusable across transports.

---

## ADR-004 — Immutable Metadata

### Status

Accepted

### Context

Runtime repeatedly consumes metadata.

Mutable metadata increases complexity and thread-safety concerns.

### Decision

Every metadata object is immutable.

Examples include:

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata

Metadata is created once and shared for the application's lifetime.

### Consequences

- Thread-safe
- Singleton lifetime
- Predictable behavior
- Easier testing

---

## ADR-005 — Immutable Execution Plans

### Status

Accepted

### Context

Execution should not modify planning decisions.

### Decision

QueryPlan and MutationPlan are immutable.

Planning performs analysis.

Runtime performs execution.

### Consequences

Runtime becomes deterministic and easier to reason about.

---

## ADR-006 — Generated Materializers

### Status

Accepted

### Context

Reflection-based materialization is slower and incompatible with Native AOT.

### Decision

The Generator emits dedicated materializers for every model.

Runtime invokes generated materializers directly.

### Consequences

- No reflection
- Better performance
- Easier debugging
- Native AOT compatibility

---

## ADR-007 — SQL Is a Serialization Layer

### Status

Accepted

### Context

Planning determines execution semantics.

SQL should not duplicate planning logic.

### Decision

SQL converts immutable plans into dialect-specific SQL.

It performs no metadata discovery or semantic analysis.

### Consequences

Clear separation between planning and serialization.

---

## ADR-008 — GraphQL Is a Transport

### Status

Accepted

### Context

GraphQL frameworks often mix transport concerns with execution.

### Decision

GraphQL only:

- Builds schemas
- Parses requests
- Invokes planners
- Calls Runtime

Execution occurs entirely within Runtime.

### Consequences

The same Runtime can support GraphQL, gRPC, Web API, and future transports.

---

## ADR-009 — Dependency Inversion for Generated Code

### Status

Accepted

### Context

Generated code should not dictate Runtime architecture.

### Decision

Generated implementations satisfy Foundation interfaces.

Examples include:

- IMetadataProvider
- IPlannerRegistry
- IEntityMaterializer
- IEntityDematerializer

### Consequences

Generated code becomes replaceable and testable.

---

## ADR-010 — Stable Identifier Allocation

### Status

Accepted

### Context

Changing identifier values unnecessarily creates noisy diffs and instability.

### Decision

Identifiers are allocated deterministically after validation.

Allocation order should remain stable between builds unless the model changes.

### Consequences

Cleaner generated code and more predictable version control history.

---

## ADR-011 — Transport Independence

### Status

Accepted

### Context

CoffeeBeanery is intended to support multiple client technologies.

### Decision

Runtime and SQL remain transport agnostic.

GraphQL, gRPC, and Web API become thin adapters over the same execution engine.

### Consequences

New transports can be introduced without modifying Runtime.

---

## ADR-012 — Native AOT Compatibility

### Status

Accepted

### Context

Native AOT imposes restrictions on reflection and runtime code generation.

### Decision

CoffeeBeanery avoids:

- Reflection
- Expression compilation
- Runtime metadata discovery
- Dynamic proxy generation

Generated code replaces these mechanisms.

### Consequences

Applications remain compatible with Native AOT while retaining high performance.

---

## Summary

These architectural decisions establish the core principles of CoffeeBeanery:

- Compile-time first
- Immutable metadata
- Immutable execution plans
- Dependency inversion
- Transport independence
- Generated implementations
- Native AOT compatibility
- Clear project boundaries

Future architectural changes should be evaluated against these principles to preserve the framework's long-term consistency and maintainability.

---

## Related Documentation

- [Architecture](../02-Architecture/README.md)
- [Contributing → ADR Process](../12-Contributing/ADR-Process.md)
- [Roadmap](Roadmap.md)

---

← Previous: [Reference](README.md)  |  Next: [FAQ](FAQ.md) →
