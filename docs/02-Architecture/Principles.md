[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Principles**

# Principles

## Contents

- [The Five Core Principles](#the-five-core-principles)
- [Extended Engineering Principles](#extended-engineering-principles)

---

## The Five Core Principles

Every other principle in this document is a refinement of these five. If a design decision
can't be justified by at least one of them, it doesn't belong in Foundgine.

**1. Business First**
The domain is the source of truth. Infrastructure exists to serve it.

**2. Compile-Time by Default**
Discover as much as possible during compilation. Avoid runtime reflection and dynamic
behavior whenever practical.

**3. Deterministic Execution**
Every request should execute through a known, generated execution plan. Predictability is
more valuable than hidden magic.

**4. Provider-Based Architecture**
Execution is delegated to providers. Today that may be PostgreSQL. Tomorrow it may be
SQL Server, Kafka, Temporal, Redis, or something else. The planner doesn't change.

**5. Transport Agnostic**
GraphQL is not special. Neither is REST. Neither is gRPC. They are simply ways of entering
the execution engine.

## Extended Engineering Principles

These are the day-to-day engineering principles that fall out of the five core principles
above — stable guidance for anyone implementing a new provider, transport, or generator
stage.

> This document captures the fundamental engineering principles that guide every architectural and implementation decision within Foundgine. These principles are intentionally long-lived and should remain stable even as individual implementations evolve.

---

### Introduction

Foundgine is designed around a simple idea:

> **Move complexity to compile time so runtime can remain simple, deterministic, and fast.**

Every architectural decision should reinforce this objective.

---

### 1. Compile-Time First

Anything that can be computed during compilation should never be computed during execution.

Examples include:

- Metadata discovery
- Relationship resolution
- Identifier allocation
- Planner generation
- Materializer generation
- Dependency analysis

The Runtime should execute prepared artifacts rather than discover information dynamically.

---

### 2. Runtime Simplicity

Runtime exists to execute.

It should never perform:

- Reflection
- Metadata discovery
- Source analysis
- Dynamic code generation
- Attribute parsing

Execution should always operate on immutable, precomputed inputs.

---

### 3. Single Responsibility

Every architectural layer owns one responsibility.

| Layer | Responsibility |
|---------|----------------|
| Foundation | Contracts |
| Runtime | Execution |
| SQL | SQL serialization |
| Generator | Compile-time analysis |
| GraphQL | Transport |
| Generated Code | Precomputed data |

Responsibilities should not overlap.

---

### 4. Dependency Inversion

High-level components should depend on abstractions rather than generated implementations.

Instead of:

```
Runtime

↓

GeneratedMetadata
```

Prefer:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

Generated code becomes a replaceable implementation rather than an architectural dependency.

---

### 5. Immutable Metadata

Metadata represents facts about the application.

Facts should not change while the application is running.

Metadata objects should therefore be:

- Immutable
- Thread-safe
- Singleton
- Shared

Examples include:

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata

---

### 6. Immutable Execution Plans

Planning determines execution.

Execution should not modify planning decisions.

QueryPlan and MutationPlan should therefore be immutable representations of work to perform.

---

### 7. Explicit Architecture

Dependencies should always be visible.

Hidden dependencies, service locators, and implicit behavior should be avoided.

Architecture should be understandable by reading project references.

---

### 8. Transport Independence

GraphQL is one transport—not the framework.

The same Runtime should execute requests originating from:

- GraphQL
- gRPC
- REST
- CLI
- Background services

Execution semantics remain identical regardless of transport.

---

### 9. Storage Independence

Planning should remain independent of storage engines.

Only SQL serialization changes between providers.

Potential providers include:

- PostgreSQL
- SQL Server
- MySQL
- SQLite
- CockroachDB

The planner should not require modification.

---

### 10. Deterministic Generation

Running the Generator twice on identical source code should produce identical generated output.

Deterministic generation simplifies:

- Debugging
- Snapshot testing
- Source control
- Build reproducibility

---

### 11. Native AOT Compatibility

Native AOT is not a separate feature.

It is a consequence of good architecture.

Avoid:

- Reflection
- Runtime IL generation
- Dynamic proxies
- Expression compilation

Prefer generated implementations and static dispatch.

---

### 12. Performance Through Architecture

Performance should result from architectural choices rather than isolated optimizations.

Examples include:

- Compile-time generation
- Immutable metadata
- Array indexing
- Generated materializers
- Precomputed dependency graphs

Architecture should eliminate work rather than optimize unnecessary work.

---

### 13. Composition Over Inheritance

Framework behavior should be composed through interfaces.

Prefer:

```csharp
IMetadataProvider

ISqlDialect

IGraphStrategy
```

Avoid deep inheritance hierarchies.

Composition improves flexibility and testing.

---

### 14. Predictability

Execution should be deterministic.

Given the same:

- Metadata
- QueryPlan
- MutationPlan
- Database state

the framework should produce identical results.

Predictability simplifies debugging and testing.

---

### 15. Testability

Every major component should be testable in isolation.

Foundation should not require Runtime.

Runtime should not require SQL.

SQL should not require GraphQL.

Generator output should be snapshot tested.

Architecture should naturally encourage testing.

---

### 16. Readability Over Cleverness

Code is read more often than it is written.

Prefer explicit implementations over clever abstractions.

Generated code should be understandable.

Runtime should be easy to debug.

Simple code generally performs well enough and is easier to maintain.

---

### 17. Stable Contracts

Foundation represents the public architectural vocabulary.

Changes to Foundation should be deliberate and infrequent.

Stable contracts reduce churn throughout the framework.

---

### 18. Extensibility Through Interfaces

Extension points should be explicit.

Applications should customize behavior through interfaces rather than modifying Runtime.

Examples include:

- IMetadataProvider
- ISqlDialect
- IEntityMaterializer
- IPlannerRegistry
- IGraphStrategy

---

### 19. Layer Isolation

Every layer should know only what it needs.

```
Foundation

↑

Runtime

↑

Transport
```

No layer should bypass another through direct implementation knowledge.

---

### 20. Long-Term Maintainability

Foundgine is intended to evolve over many years.

Short-term convenience should never compromise long-term architectural consistency.

When evaluating new features, prioritize:

- Simplicity
- Stability
- Explicitness
- Testability
- Determinism

over minimal implementation effort.

---

### Summary

These principles define the architectural identity of Foundgine.

They guide every decision—from project organization and source generation to SQL serialization and runtime execution.

When multiple implementation options exist, the preferred choice is the one that best preserves:

- Compile-time generation
- Immutable metadata
- Dependency inversion
- Deterministic execution
- Transport independence
- Clear architectural boundaries

By consistently applying these principles, Foundgine remains performant, maintainable, extensible, and adaptable as the framework continues to grow.

---

## Related Documentation

- [Vision](Vision.md)
- [Layers](Layers.md)
- [Foundation → Extensibility](../03-Foundation/Extensibility.md)

---

← Previous: [Vision](Vision.md)  |  Next: [Layers](Layers.md) →
