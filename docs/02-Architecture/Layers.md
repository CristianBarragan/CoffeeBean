[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Layers**

# Layers

> **Note on today's physical layout.** The target layout below (`CoffeeBeanery.Foundation`,
> `CoffeeBeanery.Runtime`, `CoffeeBeanery.Sql`, etc. as separate projects) is the direction
> the solution is organized toward. Today, Foundation, Runtime, SQL, and GraphQL concerns
> live as folders inside the single `src/CoffeeBeanery` project (`GraphQL/Core/Runtime`,
> `GraphQL/Core/Sql`, `GraphQL/Core/Mapping`, `GraphQL/Core/GraphQL`), and the mapping
> generator lives in its own project
> (`CoffeeBeanery.GraphQL.Core.Mapping.Generators` in the sample solution).
> The dependency *rules* below already hold; the project *split* is incremental. See
> [Vision → Roadmap by phase](Vision.md#roadmap-by-phase).

## Contents

- [Design Goals](#design-goals)
- [Solution Layout](#solution-layout)
- [Foundation](#foundation)
- [Runtime](#runtime)
- [SQL](#sql)
- [Mapping Generator](#mapping-generator)
- [Emitters](#emitters)
- [Generated Output](#generated-output)
- [GraphQL](#graphql)
- [gRPC (future phase — not implemented today)](#grpc-future-phase--not-implemented-today)
- [Web API (future phase — not implemented today)](#web-api-future-phase--not-implemented-today)
- [Test Projects](#test-projects)

---

> This document defines the recommended repository layout for the CoffeeBeanery framework. The structure is designed to enforce architectural boundaries, support independent evolution of components, and simplify long-term maintenance.

---

## Design Goals

The repository is organized around the following principles:

- Clear dependency direction
- Single responsibility per project
- Transport independence
- Compile-time generation
- Native AOT compatibility
- Minimal project coupling

Every project should have one primary purpose.

---

## Solution Layout

```
CoffeeBeanery.sln

src/

    CoffeeBeanery.Foundation/

    CoffeeBeanery.Runtime/

    CoffeeBeanery.Sql/

    CoffeeBeanery.Mapping.Generators/

    CoffeeBeanery.GraphQL/

    CoffeeBeanery.Grpc/

    CoffeeBeanery.WebApi/

tests/

    CoffeeBeanery.Foundation.Tests/

    CoffeeBeanery.Runtime.Tests/

    CoffeeBeanery.Sql.Tests/

    CoffeeBeanery.Mapping.Generators.Tests/

    CoffeeBeanery.GraphQL.Tests/

    CoffeeBeanery.Grpc.Tests/

    CoffeeBeanery.WebApi.Tests/
```

Each project should be independently buildable and testable.

---

## Foundation

```
CoffeeBeanery.Foundation

Metadata/

Planning/

Interfaces/

Ids/

Primitives/

Exceptions/

Extensions/
```

Contains:

- Contracts
- Metadata
- Planning models
- Identifiers
- Primitive value objects

Never contains:

- Roslyn
- SQL
- Runtime
- GraphQL
- Generated code

Foundation is the most stable project in the solution.

---

## Runtime

```
CoffeeBeanery.Runtime

Planner/

Execution/

Mutation/

Query/

Materialization/

Services/

Interceptors/

DependencyInjection/
```

Contains:

- Query execution
- Mutation execution
- Runtime services
- Execution context
- Materialization coordination

Depends only on:

```
Foundation
```

---

## SQL

```
CoffeeBeanery.Sql

PostgreSql/

Builders/

Visitors/

Dialects/

Readers/

Writers/
```

Contains:

- SQL writers
- SQL readers
- SQL dialects
- SQL builders
- SQL visitors

Depends on:

```
Foundation

Runtime
```

---

## Mapping Generator

```
CoffeeBeanery.Mapping.Generators

Parser/

Validation/

Model/

Passes/

Emit/

Utilities/
```

Contains:

- Incremental generator
- Mapping parser
- Validation
- Identifier allocation
- Code emitters

Depends on:

```
Foundation

Roslyn
```

Never depends on Runtime.

---

## Emitters

Recommended emitter organization:

```
Emit/

IdEmitter.cs

MetadataEmitter.cs

PlannerEmitter.cs

MaterializerEmitter.cs

DematerializerEmitter.cs

InterceptorEmitter.cs

DependencyInjectionEmitter.cs

RuntimeRegistryEmitter.cs
```

Each emitter generates one category of source code.

---

## Generated Output

Generated files should resemble:

```
Generated/

GeneratedEntityIds.cs

GeneratedMetadataProvider.cs

GeneratedPlannerRegistry.cs

GeneratedMaterializers.cs

GeneratedDematerializers.cs

GeneratedInterceptors.cs

GeneratedServiceCollectionExtensions.cs
```

Generated code should contain registrations and precomputed data, not business logic.

---

## GraphQL

```
CoffeeBeanery.GraphQL

Schema/

Types/

Resolvers/

Middleware/

DependencyInjection/
```

Contains only GraphQL-specific concerns.

Depends on:

```
Foundation

Runtime
```

---

## gRPC *(future phase — not implemented today)*

```
CoffeeBeanery.Grpc

Services/

Protobuf/

Mapping/

DependencyInjection/
```

Contains only gRPC-specific integration.

Depends on:

```
Foundation

Runtime
```

---

## Web API *(future phase — not implemented today)*

```
CoffeeBeanery.WebApi

Controllers/

MinimalApis/

Filters/

DependencyInjection/
```

Contains ASP.NET Core transport logic only.

Depends on:

```
Foundation

Runtime
```

---

## Test Projects

Every production project should have a corresponding test project.

Recommended layout:

```
tests/

CoffeeBeanery.Foundation.Tests

CoffeeBeanery.Runtime.Tests

CoffeeBeanery.Sql.Tests

CoffeeBeanery.Mapping.Generators.Tests

CoffeeBeanery.GraphQL.Tests

CoffeeBeanery.Grpc.Tests

CoffeeBeanery.WebApi.Tests
```

Tests should mirror the production structure where practical.

---

## Dependency Graph

The intended dependency graph is:

```
                 Foundation
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   Runtime           SQL      Mapping.Generators
      ▲               ▲               │
      │               │               │
      └───────────────┼───────────────┘
                      │
          Generated Runtime Components
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   GraphQL          gRPC          WebApi
```

Dependencies should always point toward more stable layers.

Circular project references should never be introduced.

---

## Dependency Rules

The following rules should always hold:

| Project | Allowed Dependencies |
|---------|-----------------------|
| Foundation | None |
| Runtime | Foundation |
| SQL | Foundation, Runtime |
| Mapping.Generators | Foundation, Roslyn |
| GraphQL | Foundation, Runtime |
| gRPC | Foundation, Runtime |
| WebApi | Foundation, Runtime |

Generated code depends only on Foundation contracts and is consumed through Dependency Injection.

---

## Long-Term Evolution

This layout enables future additions without disturbing existing layers.

Potential new projects include:

```
CoffeeBeanery.Mongo

CoffeeBeanery.Cosmos

CoffeeBeanery.Redis

CoffeeBeanery.Elasticsearch

CoffeeBeanery.Blazor

CoffeeBeanery.OpenApi

CoffeeBeanery.Cli
```

Each new project integrates through Foundation contracts rather than modifying Runtime.

---

## Summary

The repository structure reinforces CoffeeBeanery's architectural principles:

- Stable Foundation contracts
- Transport-agnostic Runtime
- Database-specific SQL layer
- Compile-time source generation
- Thin transport adapters
- Dependency inversion
- Clear project boundaries
- Long-term maintainability

By organizing the solution around responsibilities rather than technologies, CoffeeBeanery remains modular, extensible, and adaptable as the framework grows.

---

## Related Documentation

- [Vision](Vision.md)
- [Dependency Graph](Dependency-Graph.md)
- [Foundation](../03-Foundation/README.md)
- [Persistence](../08-Persistence/README.md)

---

← Previous: [Principles](Principles.md)  |  Next: [Request Pipeline](Request-Pipeline.md) →
