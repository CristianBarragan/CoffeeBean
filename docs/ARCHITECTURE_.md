# Architecture

> This document describes the architectural principles of CoffeeBeanery and the responsibilities of each project within the solution.

---

# Philosophy

CoffeeBeanery is designed around a single principle:

> **Anything that can be known at compile time should never be discovered at runtime.**

Instead of relying on reflection, expression trees, or runtime model discovery, the framework performs nearly all analysis during compilation.

Compilation produces strongly typed runtime components that execute directly without additional interpretation.

This approach provides several benefits:

- Native AOT compatibility
- Fast application startup
- Predictable execution
- Minimal allocations
- Elimination of reflection-based metadata discovery

---

# High-Level Architecture

```
                 Application Models
                        │
                        ▼
              Mapping Attributes
                        │
                        ▼
        Roslyn Incremental Source Generator
                        │
                        ▼
          Generated Runtime Components
                        │
                        ▼
              Runtime Query Planner
                        │
                        ▼
                 SQL Generation
                        │
                        ▼
               PostgreSQL / AGE
                        │
                        ▼
                Materialized Objects
```

The architecture is intentionally layered.

Each layer has a single responsibility and communicates only through contracts defined in the Foundation project.

---

# Layered Design

The solution is divided into seven logical layers.

```
Application
      │
      ▼
Transport
      │
      ▼
Runtime
      │
      ▼
SQL
      │
      ▼
Foundation
```

Compilation introduces an additional layer:

```
Application Models
        │
        ▼
Incremental Generator
        │
        ▼
Generated Code
        │
        ▼
Runtime
```

Generated code is not considered part of the runtime.

Instead, it behaves as an implementation of contracts defined in Foundation.

---

# Project Layout

```
CoffeeBeanery.sln

src/

CoffeeBeanery.Foundation
CoffeeBeanery.Runtime
CoffeeBeanery.Sql
CoffeeBeanery.Mapping.Generators
CoffeeBeanery.GraphQL
CoffeeBeanery.Grpc
CoffeeBeanery.WebApi

tests/
```

Each project has a clearly defined purpose.

Projects should not accumulate unrelated responsibilities.

---

# Foundation

Foundation is the core of the framework.

Every other project depends on Foundation.

Foundation defines contracts but performs no execution.

Typical contents include:

```
Metadata

Interfaces

Planning

Identifiers

Runtime Primitives
```

Foundation intentionally contains no:

- SQL generation
- Roslyn APIs
- GraphQL types
- ASP.NET dependencies
- Generated code
- Runtime services

This makes Foundation reusable by every transport.

---

# Runtime

Runtime executes query and mutation plans.

Responsibilities include:

- Query planning
- Mutation planning
- Runtime execution
- Materialization
- Dependency resolution
- Service orchestration

Runtime never parses attributes.

Runtime never reads Roslyn symbols.

Runtime never constructs metadata dynamically.

Instead, it consumes immutable metadata produced during compilation.

---

# SQL

The SQL project converts runtime plans into executable SQL.

Responsibilities include:

- PostgreSQL generation
- Apache AGE support
- SQL builders
- SQL visitors
- SQL dialect abstraction

SQL should never understand GraphQL.

Its input is always a runtime plan.

Its output is always SQL.

---

# Mapping Generator

The Mapping Generator is the compile-time engine of CoffeeBeanery.

It performs:

- Model discovery
- Attribute parsing
- Validation
- Relationship analysis
- Metadata generation
- Planner generation
- Materializer generation

The generator has no dependency on Runtime.

Instead, it emits implementations of Foundation contracts.

---

# Generated Components

Compilation produces generated code.

Typical generated artifacts include:

```
GeneratedEntityIds

GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedInterceptors
```

Generated code contains almost no business logic.

Its purpose is to expose precomputed data structures that eliminate runtime reflection and expensive initialization.

---

# Dependency Inversion

The architecture follows the Dependency Inversion Principle.

Instead of Runtime depending directly on generated static classes:

```
Runtime
    │
    ▼
GeneratedMetadata
```

the dependency is inverted:

```
Foundation
      ▲
      │
Runtime
      ▲
      │
GeneratedMetadataProvider
      ▲
      │
Incremental Generator
```

Runtime depends only on interfaces.

Generated code becomes a plug-in that satisfies those interfaces.

This inversion allows the runtime to remain reusable across multiple transports and future metadata sources.