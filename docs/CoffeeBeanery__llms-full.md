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

# Architecture Decisions (ADR)

> This document records the major architectural decisions that shape the CoffeeBeanery framework. It is intended to provide context for contributors and future maintainers, explaining not only **what** the architecture is, but **why** specific design choices were made.

---

# ADR-001 — Compile-Time First Architecture

## Status

Accepted

## Context

Traditional GraphQL frameworks perform extensive runtime analysis using reflection, expression trees, and dynamic code generation. This increases startup time, memory usage, and complexity while limiting compatibility with Native AOT.

## Decision

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

## Consequences

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

# ADR-002 — Foundation Owns Contracts

## Status

Accepted

## Context

Runtime, SQL, GraphQL, and generated code require a common vocabulary.

Without a dedicated foundation layer, dependencies become cyclic and implementations leak across project boundaries.

## Decision

Foundation defines:

- Metadata
- Planning primitives
- Interfaces
- Runtime primitives
- Identifiers

Foundation references no other CoffeeBeanery project.

## Consequences

Every project shares the same contracts while remaining loosely coupled.

---

# ADR-003 — Runtime Depends on Interfaces

## Status

Accepted

## Context

The original Runtime directly referenced generated static classes such as:

```csharp
GeneratedMetadata.GetEntity(...)
```

This tightly coupled Runtime to generated code.

## Decision

Runtime depends on abstractions instead.

Example:

```csharp
IMetadataProvider
```

implemented by:

```csharp
GeneratedMetadataProvider
```

## Consequences

Generated code becomes a plug-in rather than a dependency.

Runtime becomes reusable across transports.

---

# ADR-004 — Immutable Metadata

## Status

Accepted

## Context

Runtime repeatedly consumes metadata.

Mutable metadata increases complexity and thread-safety concerns.

## Decision

Every metadata object is immutable.

Examples include:

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata

Metadata is created once and shared for the application's lifetime.

## Consequences

- Thread-safe
- Singleton lifetime
- Predictable behavior
- Easier testing

---

# ADR-005 — Immutable Execution Plans

## Status

Accepted

## Context

Execution should not modify planning decisions.

## Decision

QueryPlan and MutationPlan are immutable.

Planning performs analysis.

Runtime performs execution.

## Consequences

Runtime becomes deterministic and easier to reason about.

---

# ADR-006 — Generated Materializers

## Status

Accepted

## Context

Reflection-based materialization is slower and incompatible with Native AOT.

## Decision

The Generator emits dedicated materializers for every model.

Runtime invokes generated materializers directly.

## Consequences

- No reflection
- Better performance
- Easier debugging
- Native AOT compatibility

---

# ADR-007 — SQL Is a Serialization Layer

## Status

Accepted

## Context

Planning determines execution semantics.

SQL should not duplicate planning logic.

## Decision

SQL converts immutable plans into dialect-specific SQL.

It performs no metadata discovery or semantic analysis.

## Consequences

Clear separation between planning and serialization.

---

# ADR-008 — GraphQL Is a Transport

## Status

Accepted

## Context

GraphQL frameworks often mix transport concerns with execution.

## Decision

GraphQL only:

- Builds schemas
- Parses requests
- Invokes planners
- Calls Runtime

Execution occurs entirely within Runtime.

## Consequences

The same Runtime can support GraphQL, gRPC, Web API, and future transports.

---

# ADR-009 — Dependency Inversion for Generated Code

## Status

Accepted

## Context

Generated code should not dictate Runtime architecture.

## Decision

Generated implementations satisfy Foundation interfaces.

Examples include:

- IMetadataProvider
- IPlannerRegistry
- IEntityMaterializer
- IEntityDematerializer

## Consequences

Generated code becomes replaceable and testable.

---

# ADR-010 — Stable Identifier Allocation

## Status

Accepted

## Context

Changing identifier values unnecessarily creates noisy diffs and instability.

## Decision

Identifiers are allocated deterministically after validation.

Allocation order should remain stable between builds unless the model changes.

## Consequences

Cleaner generated code and more predictable version control history.

---

# ADR-011 — Transport Independence

## Status

Accepted

## Context

CoffeeBeanery is intended to support multiple client technologies.

## Decision

Runtime and SQL remain transport agnostic.

GraphQL, gRPC, and Web API become thin adapters over the same execution engine.

## Consequences

New transports can be introduced without modifying Runtime.

---

# ADR-012 — Native AOT Compatibility

## Status

Accepted

## Context

Native AOT imposes restrictions on reflection and runtime code generation.

## Decision

CoffeeBeanery avoids:

- Reflection
- Expression compilation
- Runtime metadata discovery
- Dynamic proxy generation

Generated code replaces these mechanisms.

## Consequences

Applications remain compatible with Native AOT while retaining high performance.

---

# Summary

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

# Repository Structure

> This document defines the recommended repository layout for the CoffeeBeanery framework. The structure is designed to enforce architectural boundaries, support independent evolution of components, and simplify long-term maintenance.

---

# Design Goals

The repository is organized around the following principles:

- Clear dependency direction
- Single responsibility per project
- Transport independence
- Compile-time generation
- Native AOT compatibility
- Minimal project coupling

Every project should have one primary purpose.

---

# Solution Layout

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

# Foundation

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

# Runtime

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

# SQL

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

# Mapping Generator

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

# Emitters

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

# Generated Output

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

# GraphQL

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

# gRPC

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

# Web API

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

# Test Projects

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

# Dependency Graph

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

# Dependency Rules

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

# Long-Term Evolution

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

# Summary

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

# Contributing Guide

> Welcome to CoffeeBeanery.

This document explains the development workflow, coding standards, architectural expectations, and contribution guidelines for everyone working on the project.

The goal is consistency over cleverness.

---

# Philosophy

CoffeeBeanery is built around a few simple principles.

- Compile-time first
- Immutable by default
- Native AOT friendly
- Transport agnostic
- Dependency inversion
- Single responsibility
- Explicit architecture

Every contribution should reinforce these principles.

---

# Before Contributing

Before opening a Pull Request, contributors should read:

- Architecture.md
- Foundation.md
- Runtime.md
- SQL.md
- Generator.md
- Planning.md
- ADR.md

Understanding the architecture is far more important than understanding individual implementations.

---

# Development Workflow

Recommended workflow:

```
Fork Repository

↓

Create Branch

↓

Implement Feature

↓

Run Tests

↓

Run Generator Tests

↓

Open Pull Request

↓

Review

↓

Merge
```

Every Pull Request should focus on one logical change.

---

# Coding Standards

CoffeeBeanery favors readability.

Prefer:

```csharp
if (condition)
{
    Execute();
}
```

instead of compact syntax that is difficult to debug.

Avoid unnecessary cleverness.

Code should be obvious.

---

# Naming

Use descriptive names.

Good:

```
MutationPlanner

GeneratedMetadataProvider

EntityMetadata

QueryExecutor
```

Avoid abbreviations.

Bad:

```
Mgr

Ctx

Tmp

Util
```

---

# Single Responsibility

Each class should have one responsibility.

Good:

```
MetadataEmitter

PlannerEmitter

MaterializerEmitter
```

Avoid:

```
EverythingEmitter
```

Small focused classes are easier to test.

---

# Immutability

Prefer immutable objects.

Example:

```csharp
public sealed class EntityMetadata
{
    public ushort Id { get; }

    public string Name { get; }

    public ImmutableArray<ColumnMetadata> Columns { get; }
}
```

Mutable state should be minimized.

---

# Dependency Direction

Always verify project references.

Correct:

```
Foundation

↑

Runtime

↑

GraphQL
```

Incorrect:

```
Foundation

↓

Runtime
```

Dependencies always point toward more stable layers.

---

# Generated Code

Generated code should be:

- deterministic
- simple
- readable
- allocation friendly

Generated files should not contain business logic.

They should expose precomputed data and registrations.

---

# Runtime

Runtime code should never:

- parse attributes
- inspect Roslyn
- perform reflection
- build metadata

If Runtime needs information, it should already exist in metadata.

---

# SQL

SQL code should only translate execution plans.

It should not:

- validate requests
- resolve joins
- inspect CLR types
- parse GraphQL

Those responsibilities belong elsewhere.

---

# Foundation

Foundation is the most stable project.

Changes should be rare.

Before modifying Foundation, ask:

> Will this type be shared by multiple projects?

If not, it probably belongs somewhere else.

---

# Testing

Every feature should include tests where practical.

Recommended categories:

```
Unit Tests

Integration Tests

Snapshot Tests

Generator Tests

Regression Tests
```

Generator changes should include snapshot tests to prevent accidental output changes.

---

# Performance

Performance is a first-class concern.

Prefer:

- immutable collections
- generated code
- direct access
- value types where appropriate

Avoid:

- reflection
- repeated allocations
- repeated metadata lookups
- unnecessary LINQ in hot paths

Measure performance before introducing complexity.

---

# Documentation

New architectural features should include documentation updates.

Recommended locations:

- ADR
- Architecture
- Runtime
- Foundation
- SQL
- Generator

Documentation should evolve alongside the codebase.

---

# Pull Requests

A good Pull Request should:

- Solve one problem
- Include tests
- Preserve architecture
- Keep commits focused
- Explain architectural impact

Large unrelated changes should be split into multiple PRs.

---

# Review Checklist

Before approving a Pull Request, reviewers should verify:

- Correct dependency direction
- No runtime reflection
- No architectural boundary violations
- Tests updated
- Documentation updated
- Generated output remains deterministic
- Native AOT compatibility preserved

---

# Long-Term Vision

CoffeeBeanery aims to become a reusable data framework that supports multiple transports and storage engines without compromising architectural clarity.

Contributors should prioritize maintainability over short-term convenience.

Every line of code should make the framework easier to understand, extend, and evolve.

---

# Summary

Contributing to CoffeeBeanery is not only about writing code.

It is about preserving a consistent architecture.

By following the project's principles—compile-time generation, immutable contracts, transport independence, and dependency inversion—contributors help ensure the framework remains performant, maintainable, and scalable for years to come.

# Coding Standards

> This document defines the coding conventions and implementation guidelines used throughout the CoffeeBeanery framework.

The purpose of these standards is consistency, maintainability, performance, and architectural clarity—not personal preference.

---

# General Principles

Code should be:

- Readable
- Predictable
- Deterministic
- Testable
- Allocation-conscious
- Native AOT friendly

When faced with two implementations of equal performance, always choose the simpler one.

---

# Architecture First

Every implementation should respect project boundaries.

```
Foundation

↑

Runtime

↑

GraphQL
```

Never introduce shortcuts that violate dependency direction.

Architectural consistency is more important than reducing a few lines of code.

---

# Single Responsibility

Every type should have one clearly defined responsibility.

Good:

```
QueryPlanner

MutationPlanner

MetadataEmitter

PostgresSqlWriter
```

Avoid:

```
FrameworkManager

UtilityService

CommonHelper
```

If a class requires multiple paragraphs to explain, it likely has too many responsibilities.

---

# File Organization

One public type per file.

Example:

```
EntityMetadata.cs

QueryPlanner.cs

GeneratedMetadataProvider.cs
```

Avoid grouping unrelated public types in the same file.

---

# Namespaces

Namespaces should reflect responsibilities.

Preferred:

```
CoffeeBeanery.Foundation.Metadata

CoffeeBeanery.Runtime.Query

CoffeeBeanery.Sql.Builders
```

Avoid catch-all namespaces such as:

```
Common

Helpers

Utilities

Misc
```

---

# Naming

Names should describe intent.

Prefer:

```csharp
ResolveJoinMetadata()

BuildMutationPlan()

WriteConflictClause()
```

Instead of:

```csharp
Resolve()

Build()

Write()
```

Variables should also be descriptive.

Good:

```csharp
entityMetadata

columnReference

joinMetadata
```

Avoid:

```csharp
x

tmp

obj

data
```

---

# Method Size

Methods should generally perform one logical task.

Large methods should be decomposed into private helpers.

Instead of:

```
BuildEverything()
```

Prefer:

```
ResolveMetadata()

BuildProjection()

BuildOrdering()

BuildFilters()
```

Small methods are easier to understand and test.

---

# Immutability

Prefer immutable types.

Example:

```csharp
public sealed class EntityMetadata
{
    public ushort Id { get; }

    public string Name { get; }

    public ImmutableArray<ColumnMetadata> Columns { get; }
}
```

Mutable state should be limited to execution-specific objects.

---

# Collections

Prefer immutable collections for metadata.

Examples:

```csharp
ImmutableArray<T>

ImmutableDictionary<TKey,TValue>
```

Use mutable collections only during construction.

---

# Exceptions

Throw exceptions only for exceptional situations.

Validation errors should occur during planning or generation whenever possible.

Runtime should rarely encounter invalid metadata.

---

# Pattern Matching

Use pattern matching when it improves readability.

Example:

```csharp
if (node is JoinNode join)
{
    ...
}
```

Avoid overly clever nested patterns that reduce clarity.

---

# LINQ

LINQ is appropriate for setup and planning code.

Avoid heavy LINQ inside hot execution paths.

Prefer explicit loops where performance matters.

Example:

```csharp
foreach (var column in columns)
{
    ...
}
```

instead of deeply chained LINQ expressions.

---

# String Handling

Avoid repeated string concatenation.

Prefer:

```csharp
StringBuilder
```

for SQL generation.

Generated SQL should minimize intermediate allocations.

---

# SQL Writers

SQL writers should remain declarative.

Prefer:

```
AppendSelect()

AppendJoin()

AppendOrdering()
```

Avoid large monolithic methods that build an entire statement in one block.

---

# Comments

Comments should explain **why**, not **what**.

Good:

```csharp
// Preserve deterministic alias ordering for snapshot stability.
```

Avoid:

```csharp
// Increment i.
i++;
```

Code should be self-explanatory whenever possible.

---

# Dependency Injection

Always depend on abstractions.

Good:

```csharp
IMetadataProvider
```

Avoid:

```csharp
GeneratedMetadata
```

Runtime should never know concrete generated implementations.

---

# Performance

Performance-sensitive code should:

- Avoid reflection
- Avoid boxing
- Avoid repeated metadata lookups
- Avoid unnecessary allocations
- Prefer direct indexing where appropriate

Optimize based on measurement rather than assumptions.

---

# Testing

Public behavior should be testable.

Generated output should be verified with snapshot tests.

Runtime should have focused unit tests for execution logic.

SQL writers should have deterministic SQL output tests.

---

# Documentation

Public APIs should include XML documentation where it improves discoverability.

Architectural changes should also update:

- ADRs
- Architecture documentation
- Relevant design guides

Documentation is part of the codebase and should remain current.

---

# Summary

CoffeeBeanery values clarity over cleverness.

Consistent naming, immutable models, strict architectural boundaries, and compile-time generation are more important than minimizing line counts or introducing unnecessary abstractions.

These standards help ensure the framework remains maintainable, performant, and approachable as it continues to grow.

# Testing Strategy

> CoffeeBeanery relies on deterministic testing at every architectural layer. Because the framework performs extensive compile-time generation, testing must validate both generated artifacts and runtime behavior.

---

# Philosophy

Testing should mirror the architecture.

```
Foundation

↓

Generator

↓

Runtime

↓

SQL

↓

Transport
```

Every layer has its own responsibilities and should be tested independently.

Avoid relying solely on end-to-end integration tests.

---

# Testing Pyramid

```
               End-to-End
            Integration Tests
             Snapshot Tests
               Unit Tests
```

The majority of tests should be unit tests.

Integration tests validate interactions between components.

Snapshot tests validate generated code.

---

# Foundation Tests

Foundation is primarily composed of immutable contracts.

Typical tests include:

- Metadata construction
- Identifier behavior
- Value object equality
- Planning primitives
- Serialization
- Validation helpers

Foundation tests should never require a database.

---

# Generator Tests

The Generator requires the largest test surface.

Recommended categories:

```
Parser Tests

↓

Validation Tests

↓

Relationship Tests

↓

Identifier Allocation Tests

↓

Metadata Generation Tests

↓

Snapshot Tests
```

Each stage should be tested independently.

---

# Parser Tests

Parser tests verify discovery of application models.

Example scenarios:

- Entity detection
- Property discovery
- Graph discovery
- Join discovery
- Lookup discovery

Parser tests should isolate Roslyn analysis from code generation.

---

# Validation Tests

Validation should reject invalid models.

Examples:

- Duplicate entities
- Duplicate columns
- Circular relationships
- Missing keys
- Invalid lookups
- Unsupported property types

Compilation should fail with meaningful diagnostics.

---

# Identifier Tests

Identifier allocation should be deterministic.

Repeated compilation of the same models should produce identical identifiers.

Changing unrelated models should not reorder existing identifiers unnecessarily.

---

# Snapshot Tests

Generated source should be validated using snapshot tests.

Examples:

```
GeneratedMetadataProvider.cs

GeneratedMaterializers.cs

GeneratedPlannerRegistry.cs

GeneratedEntityIds.cs
```

Snapshot testing ensures generated output remains stable over time.

---

# Runtime Tests

Runtime tests verify execution behavior independently of SQL.

Examples:

- Query execution
- Mutation execution
- Dependency ordering
- Generated value propagation
- Materialization coordination
- Transaction handling

Runtime tests should replace external dependencies with test doubles where practical.

---

# SQL Tests

SQL tests validate generated SQL.

Typical assertions include:

```
QueryPlan

↓

SQL

↓

Expected Statement
```

Areas to test:

- SELECT generation
- JOIN generation
- WHERE clauses
- ORDER BY
- LIMIT/OFFSET
- INSERT
- UPDATE
- UPSERT
- RETURNING
- Graph SQL

SQL output should be deterministic.

---

# Materializer Tests

Generated materializers should be verified independently.

Typical scenarios:

- Primitive values
- Nullable values
- Nested objects
- Collections
- Identity resolution
- Graph projections

Materialization tests should not require reflection.

---

# Graph Tests

Graph planning and SQL generation should have dedicated tests.

Examples:

- Vertex traversal
- Edge traversal
- Graph merge generation
- Graph projections
- Cyclic graph validation

---

# Integration Tests

Integration tests verify collaboration between projects.

Example pipeline:

```
GraphQL Request

↓

Planner

↓

Runtime

↓

SQL

↓

Database

↓

Materialization

↓

Result
```

Integration tests should cover realistic application scenarios.

---

# Performance Tests

Performance-sensitive components should include benchmarks.

Examples:

- SQL generation
- Materialization
- Planner execution
- Metadata lookup
- Mutation dependency resolution

Benchmarks should be tracked over time to detect regressions.

---

# Regression Tests

Every reported bug should produce a regression test.

Workflow:

```
Bug Report

↓

Failing Test

↓

Fix

↓

Passing Test
```

Regression tests prevent previously resolved issues from reappearing.

---

# Native AOT Tests

Because Native AOT is a core design goal, compatibility should be validated regularly.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Generated materializers
- Metadata provider
- Planner registry

No runtime reflection should be introduced.

---

# Test Project Layout

```
tests/

CoffeeBeanery.Foundation.Tests/

CoffeeBeanery.Runtime.Tests/

CoffeeBeanery.Sql.Tests/

CoffeeBeanery.Mapping.Generators.Tests/

CoffeeBeanery.GraphQL.Tests/

CoffeeBeanery.Grpc.Tests/

CoffeeBeanery.WebApi.Tests/
```

Each test project should mirror the structure of its production project where practical.

---

# Continuous Integration

Every pull request should execute:

- Unit tests
- Generator tests
- Snapshot tests
- Integration tests
- Native AOT validation (where supported)

Builds should fail if generated snapshots change unexpectedly.

---

# Summary

CoffeeBeanery's testing strategy emphasizes deterministic behavior, compile-time validation, and architectural isolation.

By combining unit tests, snapshot tests, integration tests, and performance benchmarks, the framework can evolve confidently while preserving correctness, performance, and long-term maintainability.

# Performance Guide

> Performance is a primary design objective of CoffeeBeanery. The framework is designed to move computational work from runtime to compile time, minimizing allocations, eliminating reflection, and producing deterministic execution.

Performance improvements should preserve architectural clarity rather than introduce unnecessary complexity.

---

# Core Principles

CoffeeBeanery follows six performance principles:

- Compile-time over runtime
- Immutable metadata
- Deterministic execution
- Zero reflection
- Allocation awareness
- Cache-friendly data structures

Every optimization should support one or more of these principles.

---

# Compile-Time Optimization

The Generator performs expensive analysis during compilation.

Examples include:

```
Metadata Discovery

↓

Relationship Analysis

↓

Identifier Allocation

↓

Materializer Generation

↓

Planner Registry Generation
```

Runtime performs none of these operations.

---

# Runtime Optimization

Runtime execution should resemble:

```
Immutable Plan

↓

SQL

↓

Database

↓

Materialization
```

Execution should avoid:

- Reflection
- Metadata discovery
- Expression compilation
- Dynamic dispatch
- Runtime code generation

---

# Metadata Performance

Metadata should be:

- Immutable
- Singleton
- Shared
- Allocation-free during execution

Instead of:

```
Dictionary Lookup

↓

Column Metadata
```

prefer:

```
Array Index

↓

Column Metadata
```

whenever identifiers are contiguous.

---

# Generated Code

Generated code should prioritize direct access.

Preferred:

```csharp
return _entities[id];
```

Avoid:

```csharp
return _dictionary[id];
```

when dense arrays are sufficient.

Generated code should be straightforward and easy for the JIT to optimize.

---

# SQL Generation

SQL generation should minimize allocations.

Prefer:

```csharp
StringBuilder
```

over repeated string concatenation.

Builders should append directly to the destination buffer rather than producing intermediate strings.

---

# Materialization

Generated materializers should read values directly by ordinal.

Example:

```csharp
reader.GetInt32(0);

reader.GetString(1);

reader.GetBoolean(2);
```

Avoid resolving ordinals by column name during execution.

---

# Collections

Use immutable collections for metadata.

Use mutable collections only while constructing immutable objects.

Execution should avoid modifying metadata collections.

---

# LINQ

LINQ is appropriate during generation and planning.

Execution paths should favor explicit loops.

Preferred:

```csharp
foreach (var column in columns)
{
    ...
}
```

This reduces allocations and improves predictability in hot paths.

---

# Reflection

Reflection should be confined to compile-time analysis performed by Roslyn.

Runtime should never inspect:

- Types
- Properties
- Attributes
- Constructors

Generated code replaces reflection.

---

# Memory Allocation

Hot execution paths should avoid:

- Boxing
- Temporary collections
- Delegate allocations
- Closure allocations
- Repeated string creation

Allocation-free execution should be the default goal.

---

# Query Planning

Planning is intentionally more expensive than execution.

Planner responsibilities include:

- Join resolution
- Projection pruning
- Metadata resolution
- Alias generation
- Dependency analysis

Execution simply consumes the resulting plan.

---

# Mutation Performance

Mutation execution benefits from precomputed dependency graphs.

Instead of repeatedly analyzing relationships:

```
Mutation

↓

Planner

↓

Dependency Graph

↓

Runtime
```

Runtime walks a prepared execution graph.

---

# Graph Performance

Graph traversals should also be planned.

Traversal metadata should include:

- Vertex identifiers
- Edge identifiers
- Direction
- Depth
- Projections

Execution avoids graph discovery.

---

# Native AOT

Native AOT is a primary performance target.

Avoid:

- Reflection
- Dynamic proxies
- Runtime IL generation
- Expression compilation

Prefer generated implementations and static dispatch.

---

# Benchmarking

Benchmark the following components regularly:

- Metadata lookup
- Query planning
- Mutation planning
- SQL generation
- Materialization
- Dependency graph traversal

Benchmark regressions should be investigated before merging.

---

# Profiling

Performance investigations should rely on measurement rather than assumptions.

Useful metrics include:

- Execution time
- Allocation count
- GC pressure
- Generated SQL size
- Materialization throughput

Optimizations should target measured bottlenecks.

---

# Performance Checklist

Before merging performance-sensitive changes, verify:

- No additional allocations in hot paths
- No reflection introduced
- Metadata remains immutable
- Generated code remains deterministic
- SQL output remains stable
- Benchmarks show no regression

---

# Summary

CoffeeBeanery achieves performance through architecture rather than micro-optimizations.

By combining compile-time generation, immutable metadata, deterministic planning, generated materializers, and transport-independent execution, the framework minimizes runtime overhead while remaining maintainable and extensible.

# Native AOT Design

> Native AOT compatibility is a first-class architectural requirement for CoffeeBeanery. Every layer of the framework should be designed with ahead-of-time compilation in mind rather than treating AOT as an afterthought.

---

# Philosophy

CoffeeBeanery follows one fundamental rule:

> **If the compiler can know it, the runtime should not discover it.**

This principle naturally aligns with Native AOT.

```
Compile Time

↓

Generate Everything

↓

Runtime

↓

Execute Only
```

---

# Why Native AOT?

Native AOT provides:

- Faster startup
- Lower memory usage
- Smaller deployment footprint
- Better container performance
- Reduced cold-start latency
- Improved cloud scalability

Supporting Native AOT also encourages better architectural discipline.

---

# Design Principles

CoffeeBeanery achieves Native AOT compatibility by avoiding runtime features that require dynamic analysis.

Key principles include:

- No runtime reflection
- No runtime code generation
- No expression compilation
- No dynamic proxy generation
- No runtime metadata discovery

Everything required for execution is generated during compilation.

---

# Compile-Time Generation

The Generator produces:

```
Metadata

↓

Identifiers

↓

Planner Registry

↓

Materializers

↓

Dematerializers

↓

Runtime Registrations
```

Runtime simply consumes these generated artifacts.

---

# Reflection

Reflection should only exist inside the Incremental Generator.

Example:

```
Roslyn

↓

Generator

↓

Metadata
```

Runtime never calls:

```csharp
typeof(...)

GetProperties()

GetCustomAttributes()

Activator.CreateInstance()
```

---

# Metadata

Metadata is immutable and generated.

Instead of:

```
Reflection

↓

Metadata Discovery
```

Runtime receives:

```
GeneratedMetadataProvider

↓

EntityMetadata
```

No discovery occurs during execution.

---

# Materialization

Generated materializers eliminate runtime inspection.

Example:

```csharp
public Customer Read(DbDataReader reader)
{
    return new Customer
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1)
    };
}
```

This pattern is fully compatible with Native AOT.

---

# Dependency Injection

Runtime depends upon interfaces.

Example:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer
```

Generated implementations are registered during application startup.

No service location or runtime type scanning is required.

---

# Runtime Behavior

Runtime performs only deterministic operations.

```
Plan

↓

SQL

↓

Database

↓

Materialization
```

Execution should never modify metadata or inspect application types.

---

# Dynamic Features to Avoid

Avoid introducing:

- Reflection
- DynamicMethod
- Reflection.Emit
- Expression.Compile()
- Runtime IL generation
- Dynamic proxies
- Runtime assembly scanning

These features either reduce AOT compatibility or require additional configuration.

---

# Collections

Prefer static, immutable collections.

Examples:

```csharp
ImmutableArray<T>

ImmutableDictionary<TKey, TValue>
```

Generated metadata should be initialized once and reused for the application's lifetime.

---

# Generic Code

Prefer closed generic registrations where practical.

Avoid runtime generic construction using reflection.

Generated registries should reference concrete implementations directly.

---

# Serialization

Where serialization is required, prefer source-generated serializers.

Example:

```csharp
[JsonSerializable(typeof(Customer))]
internal partial class CoffeeBeaneryJsonContext
    : JsonSerializerContext
{
}
```

Avoid reflection-based serializers.

---

# SQL

SQL generation should remain purely deterministic.

SQL writers should consume immutable execution plans without inspecting CLR types.

This naturally aligns with Native AOT constraints.

---

# Testing Native AOT

Native AOT should be validated continuously.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Query execution
- Mutation execution
- Materialization
- Metadata resolution

These tests help prevent accidental introduction of unsupported runtime features.

---

# Performance Benefits

Designing for Native AOT also improves traditional JIT execution.

Benefits include:

- Fewer allocations
- Reduced startup work
- Simpler execution paths
- Better cache locality
- More predictable performance

Compile-time optimization benefits every deployment model.

---

# Future Considerations

As CoffeeBeanery evolves, new features should be evaluated against the following questions:

- Can this be generated at compile time?
- Does this require reflection?
- Can metadata replace runtime discovery?
- Can generated code replace dynamic behavior?
- Does this preserve deterministic execution?

If the answer favors compile-time generation, it is usually the preferred design.

---

# Summary

Native AOT is not a separate feature of CoffeeBeanery—it is a consequence of the framework's architecture.

By emphasizing compile-time generation, immutable metadata, generated materializers, dependency inversion, and deterministic execution, CoffeeBeanery remains compatible with Native AOT while delivering predictable performance across all supported transports.

# Extensibility Guide

> CoffeeBeanery is designed to be extended through well-defined contracts rather than inheritance or runtime discovery. This document describes the framework's extensibility model and identifies the supported extension points.

---

# Philosophy

CoffeeBeanery follows the **Open/Closed Principle**.

The framework should be:

- Open for extension
- Closed for modification

Applications should be able to customize behavior without changing the Runtime.

---

# Architectural Principle

Every extension point lives behind a Foundation interface.

```
Application

↓

Custom Implementation

↓

Foundation Interface

↓

Runtime
```

Runtime never depends upon application code directly.

---

# Extension Categories

CoffeeBeanery exposes extension points in several areas:

```
Metadata

Planning

SQL

Materialization

Dematerialization

Graph

Interceptors

Dependency Injection

Transports
```

Each category has a clearly defined responsibility.

---

# Metadata Providers

Metadata is supplied through `IMetadataProvider`.

```csharp
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort storageEntityId);

    ModelMetadata GetModel(ushort modelId);

    JoinMetadata? GetJoin(
        ushort leftStorageEntity,
        ushort rightStorageEntity);

    GraphMetadata? GetGraph(ushort graphId);
}
```

Most applications use the generated implementation.

Advanced scenarios may provide custom metadata sources.

---

# Planner Registry

The planner registry maps models to generated planners.

Example contract:

```csharp
public interface IPlannerRegistry
{
    QueryPlanner GetQueryPlanner(ushort modelId);

    MutationPlanner GetMutationPlanner(ushort modelId);
}
```

Generated registries are the default implementation.

---

# SQL Dialects

SQL generation is intentionally database-independent.

A dialect implementation owns provider-specific syntax.

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteReturning(...);

    void WriteConflict(...);
}
```

Potential implementations:

- PostgreSQL
- SQL Server
- MySQL
- SQLite
- Oracle

---

# SQL Writers

Applications may replace SQL writers.

Example:

```csharp
ISqlWriter
```

Possible customizations:

- Multi-tenant SQL
- Audit SQL
- Soft-delete behavior
- Vendor-specific optimizations

---

# Materializers

Materializers convert rows into CLR objects.

```csharp
IEntityMaterializer
```

Generated implementations should satisfy most scenarios.

Custom materializers may support:

- Immutable records
- Custom collections
- Domain object construction

---

# Dematerializers

Dematerializers convert CLR objects into mutation values.

```csharp
IEntityDematerializer
```

Custom implementations may support:

- Domain events
- Change tracking
- Alternate serialization

---

# Graph Strategy

Graph support is isolated behind a strategy interface.

Example:

```csharp
IGraphStrategy
```

Possible implementations:

- Apache AGE
- Neo4j bridge
- Custom graph database
- No-op implementation

This isolates graph behavior from Runtime.

---

# Interceptors

Interceptors provide lifecycle hooks.

Typical events include:

```
Before Planning

After Planning

Before SQL

After SQL

Before Execution

After Execution

Before Materialization

After Materialization
```

Interceptors should observe or augment behavior rather than replace core execution.

---

# Dependency Injection

Every generated component should be replaceable.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
                      GeneratedMetadataProvider>();
```

Applications may substitute:

```csharp
services.AddSingleton<IMetadataProvider,
                      CustomMetadataProvider>();
```

Runtime remains unchanged.

---

# Transport Extensions

Runtime is transport agnostic.

New transports can integrate by translating requests into immutable plans.

Potential transports include:

- GraphQL
- gRPC
- REST
- SignalR
- CLI
- Background workers

No Runtime changes should be required.

---

# Storage Providers

Although CoffeeBeanery currently targets PostgreSQL, the architecture supports additional storage engines.

Potential future providers:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

Each provider implements SQL abstractions while reusing the same planners and Runtime.

---

# Generator Extensions

The Mapping Generator can evolve through additional emitters.

Examples:

```
IdEmitter

MetadataEmitter

PlannerEmitter

MaterializerEmitter

InterceptorEmitter

DependencyInjectionEmitter
```

Future emitters can generate additional compile-time artifacts without affecting existing Runtime components.

---

# Best Practices

When extending CoffeeBeanery:

- Prefer interfaces over inheritance
- Preserve immutability
- Avoid reflection
- Respect project boundaries
- Keep generated code deterministic
- Register implementations through Dependency Injection

Extensions should integrate with the framework rather than bypass it.

---

# Summary

CoffeeBeanery is intentionally extensible through Foundation contracts.

By exposing clear interfaces for metadata, planning, SQL generation, materialization, graph strategies, and transports, the framework can evolve without compromising its core architecture of compile-time generation, immutable execution plans, and transport-independent Runtime.

# Roadmap

> This document describes the long-term technical roadmap for the CoffeeBeanery framework. It outlines the expected evolution of the architecture while preserving the project's core design principles.

The roadmap is intentionally organized around architectural capabilities rather than release dates.

---

# Vision

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

# Phase 1 — Foundation

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

# Phase 2 — Runtime

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

# Phase 3 — SQL

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

# Phase 4 — Incremental Generator

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

# Phase 5 — Dependency Inversion

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

# Phase 6 — GraphQL

## Goals

- Schema generation
- Resolver generation
- Middleware
- Dependency Injection
- Transport adapter

GraphQL becomes a thin layer over Runtime.

---

# Phase 7 — gRPC

## Goals

- Protobuf integration
- Service generation
- Runtime adapter

The same Runtime should execute GraphQL and gRPC requests.

---

# Phase 8 — Web API

## Goals

- Controllers
- Minimal APIs
- OpenAPI integration
- Runtime adapter

Execution remains identical to GraphQL and gRPC.

---

# Phase 9 — Additional SQL Providers

Potential providers include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

The planner remains unchanged.

Only SQL serialization changes.

---

# Phase 10 — Graph Improvements

Future graph capabilities include:

- Recursive traversals
- Variable-length paths
- Path projections
- Graph aggregation
- Graph mutation optimization
- Graph query caching

These features extend planning while preserving Runtime behavior.

---

# Phase 11 — Performance

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

# Phase 12 — Native AOT

Continue validating:

- Metadata provider
- Materializers
- Planner registry
- SQL generation
- Runtime execution

No feature should compromise Native AOT compatibility.

---

# Phase 13 — Tooling

Developer tooling may include:

- Visual Studio integration
- Roslyn analyzers
- Diagnostic visualizers
- SQL preview
- Query plan visualizer
- Graph explorer

These tools should consume generated metadata where possible.

---

# Phase 14 — Ecosystem

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

# Success Criteria

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

# Guiding Principles

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

# Summary

The CoffeeBeanery roadmap is focused on evolving the framework through compile-time generation, transport independence, and dependency inversion.

Rather than adding isolated features, each phase strengthens the overall architecture, ensuring the framework remains performant, maintainable, extensible, and capable of supporting new transports and storage providers without compromising its core design.

# FAQ

> This document answers the most common questions about CoffeeBeanery's architecture, design decisions, and development philosophy.

---

# Why does CoffeeBeanery use Source Generators?

CoffeeBeanery performs most framework analysis during compilation rather than runtime.

This includes:

- Metadata generation
- Relationship analysis
- Identifier allocation
- Materializer generation
- Planner generation
- Runtime registrations

This significantly reduces runtime work while improving startup performance and Native AOT compatibility.

---

# Why not use reflection?

Reflection introduces:

- Startup overhead
- Additional allocations
- Dynamic behavior
- Native AOT limitations
- Runtime uncertainty

Generated code provides the same information without requiring runtime discovery.

---

# Why split Foundation from Runtime?

Foundation defines contracts.

Runtime implements behavior.

Keeping them separate provides:

- Stable interfaces
- Better testing
- Dependency inversion
- Transport independence
- Cleaner project references

Foundation should never know Runtime exists.

---

# Why generate metadata?

Metadata rarely changes while an application is running.

Generating metadata once during compilation avoids repeated runtime analysis and enables immutable, singleton metadata objects.

---

# Why immutable metadata?

Immutable metadata is:

- Thread-safe
- Reusable
- Predictable
- Easy to cache
- Easy to test

Runtime never needs to modify metadata.

---

# Why immutable execution plans?

Planning determines *what* should happen.

Execution determines *when* it happens.

Separating these responsibilities simplifies Runtime and improves determinism.

---

# Why separate Planning from SQL?

Planning understands application semantics.

SQL understands database syntax.

Keeping them independent allows:

- Better testing
- Multiple SQL dialects
- Cleaner architecture
- Simpler SQL writers

---

# Why generate materializers?

Generated materializers:

- Avoid reflection
- Read values by ordinal
- Reduce allocations
- Improve performance
- Support Native AOT

Materialization becomes simple generated code.

---

# Why not build SQL inside GraphQL?

GraphQL is a transport.

Its responsibilities are:

- Schema
- Resolvers
- Request parsing

SQL belongs to the SQL project.

Keeping transport and execution separate makes Runtime reusable.

---

# Why support multiple transports?

The same execution engine should support:

- GraphQL
- gRPC
- REST
- CLI
- Background workers

Only the request translation changes.

Execution remains identical.

---

# Why use Dependency Injection?

Dependency Injection allows Runtime to depend upon interfaces rather than generated implementations.

For example:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

This improves testing and extensibility.

---

# Why avoid static generated classes?

Static classes tightly couple Runtime to generated code.

Generated implementations registered through interfaces allow:

- Mocking
- Replacement
- Testing
- Multiple implementations

---

# Why does Runtime avoid Roslyn?

Roslyn is a compile-time technology.

Runtime should execute plans—not analyze source code.

Keeping Roslyn isolated within the Generator reduces complexity and improves portability.

---

# Why prioritize Native AOT?

Native AOT aligns naturally with CoffeeBeanery's architecture.

Compile-time generation eliminates the need for:

- Reflection
- Dynamic proxies
- Runtime code generation
- Expression compilation

The resulting framework performs well in both JIT and AOT environments.

---

# Can CoffeeBeanery support databases other than PostgreSQL?

Yes.

Planning is database-independent.

Only SQL serialization changes.

Future providers may include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

---

# Can CoffeeBeanery support transports other than GraphQL?

Yes.

Runtime is transport agnostic.

Future transports may include:

- gRPC
- REST
- SignalR
- CLI
- Batch processing

---

# Why generate identifiers?

Stable generated identifiers provide:

- Fast lookups
- Array indexing
- Deterministic output
- Smaller runtime overhead

Identifiers are allocated during compilation.

---

# What belongs in Foundation?

Foundation contains:

- Metadata
- Interfaces
- Planning primitives
- Runtime primitives
- Identifiers

It intentionally excludes:

- Runtime
- SQL
- Roslyn
- GraphQL
- Generated code

---

# What belongs in Runtime?

Runtime owns:

- Query execution
- Mutation execution
- Transaction coordination
- Materialization orchestration
- Execution pipelines

Runtime should never:

- Discover metadata
- Parse attributes
- Generate SQL
- Inspect CLR models

---

# What belongs in the Generator?

The Generator performs compile-time work:

- Model discovery
- Validation
- Relationship resolution
- Metadata generation
- Materializer generation
- Planner generation
- Runtime registration generation

Generated code becomes Runtime's input.

---

# What makes CoffeeBeanery different?

CoffeeBeanery differs from many data frameworks by emphasizing:

- Compile-time analysis
- Immutable metadata
- Immutable execution plans
- Source generation
- Transport independence
- Dependency inversion
- Native AOT compatibility

The framework is designed so Runtime executes precomputed artifacts rather than discovering application structure during execution.

---

# Summary

CoffeeBeanery's design choices consistently favor compile-time computation, immutable models, clear architectural boundaries, and reusable execution components.

Understanding these principles makes the rest of the framework significantly easier to understand and extend.

# Glossary

> This glossary defines the core terminology used throughout the CoffeeBeanery framework. The terms below have specific architectural meanings and should be used consistently across documentation, code, and discussions.

---

# Entity

A physical storage object.

An Entity represents the database structure rather than the public application model.

Examples include:

- Customer
- Order
- Product

Entities map directly to storage metadata.

---

# Model

A public CLR representation exposed to the application.

A model may map to:

- one entity
- multiple entities
- graph traversals
- projections

Models are transport-independent.

---

# Storage Entity

The physical table or storage object used by SQL generation.

Every storage entity has:

- StorageEntityId
- EntityMetadata
- Columns
- Keys

---

# Field

A logical member exposed by a model.

Fields may represent:

- database columns
- computed values
- graph properties
- projections
- aggregates

Fields are not always physical columns.

---

# Column

A physical database column.

Columns belong to storage entities.

Example:

```
Customer.Name
```

becomes

```
Customer

↓

Name Column
```

---

# Column Reference

A lightweight object describing a physical column.

Typically includes:

- Entity
- Column Metadata
- Ordinal
- Identifier

Column references eliminate repeated metadata lookups.

---

# Metadata

Immutable information describing the application's data model.

Examples include:

- EntityMetadata
- ModelMetadata
- JoinMetadata
- GraphMetadata
- ColumnMetadata

Metadata is generated during compilation.

---

# Metadata Provider

The runtime source of metadata.

Example:

```csharp
IMetadataProvider
```

Typical implementation:

```csharp
GeneratedMetadataProvider
```

Runtime depends only upon the interface.

---

# Query

A read operation.

Queries produce immutable QueryPlans which Runtime executes.

---

# Mutation

A write operation.

Mutations produce immutable MutationPlans containing dependency information and SQL operations.

---

# Query Planner

Converts selections into immutable QueryPlans.

Responsibilities include:

- projection planning
- join planning
- graph planning
- metadata resolution

---

# Mutation Planner

Converts mutation requests into immutable MutationPlans.

Responsibilities include:

- dependency analysis
- relationship resolution
- ordering
- generated value propagation

---

# Query Plan

An immutable description of a read operation.

Contains everything Runtime requires for execution.

---

# Mutation Plan

An immutable description of a write operation.

Includes:

- operations
- dependencies
- projections
- graph merges

---

# Materializer

Generated code that converts database rows into CLR objects.

Materializers replace reflection.

---

# Dematerializer

Generated code that converts CLR objects into mutation values.

Dematerializers replace runtime property inspection.

---

# Planner Registry

A generated registry that maps identifiers to planners.

Runtime uses the registry to locate generated planners without reflection.

---

# Graph

A graph representation consisting of:

- vertices
- edges
- traversals

Graph metadata exists independently of relational metadata.

---

# Join

A relationship between two storage entities.

Joins are resolved during planning rather than SQL generation.

---

# Graph Strategy

A provider responsible for graph-specific SQL generation.

Example implementations:

- Apache AGE
- Custom graph providers

---

# SQL Writer

A component that converts immutable execution plans into executable SQL statements.

SQL Writers do not perform planning.

---

# SQL Dialect

Defines database-specific SQL syntax.

Examples include:

- PostgreSQL
- SQL Server
- SQLite

Only serialization changes between dialects.

---

# Generator

The Roslyn Incremental Source Generator responsible for compile-time analysis and code generation.

The Generator produces:

- metadata
- identifiers
- planners
- materializers
- registrations

---

# Generated Code

Source emitted during compilation.

Generated code should contain:

- immutable metadata
- registrations
- strongly typed implementations

Generated code should contain very little business logic.

---

# Foundation

The lowest architectural layer.

Defines:

- contracts
- metadata
- identifiers
- planning primitives

Foundation references no other CoffeeBeanery project.

---

# Runtime

The execution engine.

Consumes:

- metadata
- planners
- SQL writers
- materializers

Runtime performs execution only.

---

# SQL Layer

The serialization layer responsible for converting execution plans into SQL.

It does not understand GraphQL or CLR models.

---

# Transport

An adapter that translates external requests into Runtime plans.

Examples include:

- GraphQL
- gRPC
- REST

Transports do not execute queries.

---

# Dependency Inversion

An architectural principle where Runtime depends upon interfaces rather than generated implementations.

Example:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

This keeps Runtime reusable and testable.

---

# Native AOT

Ahead-of-Time compilation supported through compile-time generation and the avoidance of runtime reflection.

CoffeeBeanery is designed to remain fully compatible with Native AOT.

---

# Summary

Using this terminology consistently helps maintain a shared architectural language across the CoffeeBeanery codebase. Contributors should prefer these definitions when naming types, writing documentation, reviewing pull requests, and discussing future design decisions.

# Design Principles

> This document captures the fundamental engineering principles that guide every architectural and implementation decision within CoffeeBeanery. These principles are intentionally long-lived and should remain stable even as individual implementations evolve.

---

# Introduction

CoffeeBeanery is designed around a simple idea:

> **Move complexity to compile time so runtime can remain simple, deterministic, and fast.**

Every architectural decision should reinforce this objective.

---

# 1. Compile-Time First

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

# 2. Runtime Simplicity

Runtime exists to execute.

It should never perform:

- Reflection
- Metadata discovery
- Source analysis
- Dynamic code generation
- Attribute parsing

Execution should always operate on immutable, precomputed inputs.

---

# 3. Single Responsibility

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

# 4. Dependency Inversion

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

# 5. Immutable Metadata

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

# 6. Immutable Execution Plans

Planning determines execution.

Execution should not modify planning decisions.

QueryPlan and MutationPlan should therefore be immutable representations of work to perform.

---

# 7. Explicit Architecture

Dependencies should always be visible.

Hidden dependencies, service locators, and implicit behavior should be avoided.

Architecture should be understandable by reading project references.

---

# 8. Transport Independence

GraphQL is one transport—not the framework.

The same Runtime should execute requests originating from:

- GraphQL
- gRPC
- REST
- CLI
- Background services

Execution semantics remain identical regardless of transport.

---

# 9. Storage Independence

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

# 10. Deterministic Generation

Running the Generator twice on identical source code should produce identical generated output.

Deterministic generation simplifies:

- Debugging
- Snapshot testing
- Source control
- Build reproducibility

---

# 11. Native AOT Compatibility

Native AOT is not a separate feature.

It is a consequence of good architecture.

Avoid:

- Reflection
- Runtime IL generation
- Dynamic proxies
- Expression compilation

Prefer generated implementations and static dispatch.

---

# 12. Performance Through Architecture

Performance should result from architectural choices rather than isolated optimizations.

Examples include:

- Compile-time generation
- Immutable metadata
- Array indexing
- Generated materializers
- Precomputed dependency graphs

Architecture should eliminate work rather than optimize unnecessary work.

---

# 13. Composition Over Inheritance

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

# 14. Predictability

Execution should be deterministic.

Given the same:

- Metadata
- QueryPlan
- MutationPlan
- Database state

the framework should produce identical results.

Predictability simplifies debugging and testing.

---

# 15. Testability

Every major component should be testable in isolation.

Foundation should not require Runtime.

Runtime should not require SQL.

SQL should not require GraphQL.

Generator output should be snapshot tested.

Architecture should naturally encourage testing.

---

# 16. Readability Over Cleverness

Code is read more often than it is written.

Prefer explicit implementations over clever abstractions.

Generated code should be understandable.

Runtime should be easy to debug.

Simple code generally performs well enough and is easier to maintain.

---

# 17. Stable Contracts

Foundation represents the public architectural vocabulary.

Changes to Foundation should be deliberate and infrequent.

Stable contracts reduce churn throughout the framework.

---

# 18. Extensibility Through Interfaces

Extension points should be explicit.

Applications should customize behavior through interfaces rather than modifying Runtime.

Examples include:

- IMetadataProvider
- ISqlDialect
- IEntityMaterializer
- IPlannerRegistry
- IGraphStrategy

---

# 19. Layer Isolation

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

# 20. Long-Term Maintainability

CoffeeBeanery is intended to evolve over many years.

Short-term convenience should never compromise long-term architectural consistency.

When evaluating new features, prioritize:

- Simplicity
- Stability
- Explicitness
- Testability
- Determinism

over minimal implementation effort.

---

# Summary

These principles define the architectural identity of CoffeeBeanery.

They guide every decision—from project organization and source generation to SQL serialization and runtime execution.

When multiple implementation options exist, the preferred choice is the one that best preserves:

- Compile-time generation
- Immutable metadata
- Dependency inversion
- Deterministic execution
- Transport independence
- Clear architectural boundaries

By consistently applying these principles, CoffeeBeanery remains performant, maintainable, extensible, and adaptable as the framework continues to grow.

# Architecture Overview

> This document provides a high-level overview of the CoffeeBeanery architecture. It introduces the major projects, explains their responsibilities, and illustrates how requests flow through the framework.

This document should be the starting point for anyone new to the codebase.

---

# Vision

CoffeeBeanery is a compile-time-first data framework.

Rather than discovering application structure during execution, CoffeeBeanery performs analysis during compilation and generates strongly typed runtime components.

The Runtime executes these generated artifacts without relying on reflection or runtime metadata discovery.

---

# Architectural Goals

The architecture is designed to achieve the following goals:

- Compile-time analysis
- Deterministic execution
- Immutable metadata
- Transport independence
- Database abstraction
- Native AOT compatibility
- Dependency inversion
- Long-term maintainability

These goals influence every layer of the framework.

---

# High-Level Architecture

```
                Application Models
                        │
                        ▼
        Incremental Source Generator
                        │
                        ▼
        Generated Runtime Components
                        │
                        ▼
                  Runtime Engine
                        │
                        ▼
                  SQL Generation
                        │
                        ▼
                    Database
```

Transports exist above the Runtime and translate external requests into execution plans.

---

# Solution Structure

```
CoffeeBeanery.Foundation

CoffeeBeanery.Runtime

CoffeeBeanery.Sql

CoffeeBeanery.Mapping.Generators

CoffeeBeanery.GraphQL

CoffeeBeanery.Grpc

CoffeeBeanery.WebApi
```

Each project owns one architectural responsibility.

---

# Foundation

Foundation defines the framework's shared language.

It contains:

- Metadata
- Interfaces
- Planning primitives
- Runtime primitives
- Identifiers

Foundation never references any other CoffeeBeanery project.

It is the architectural root of the solution.

---

# Runtime

Runtime executes immutable plans.

Responsibilities include:

- Query execution
- Mutation execution
- Transaction coordination
- Materialization orchestration
- Graph execution

Runtime consumes Foundation contracts and generated implementations.

It performs no compile-time analysis.

---

# SQL

The SQL layer converts execution plans into SQL statements.

Responsibilities include:

- Query serialization
- Mutation serialization
- SQL dialect abstraction
- PostgreSQL support
- Graph SQL generation

SQL does not understand GraphQL, Roslyn, or CLR models.

---

# Mapping Generator

The Generator performs compile-time analysis.

Its responsibilities include:

- Model discovery
- Validation
- Relationship analysis
- Metadata generation
- Planner generation
- Materializer generation
- Runtime registration generation

Generated code becomes input for the Runtime.

---

# Generated Components

Compilation produces strongly typed runtime artifacts such as:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedEntityIds

GeneratedServiceCollectionExtensions
```

These classes contain precomputed data and registrations rather than runtime logic.

---

# Request Flow

A typical request follows this path:

```
Client

↓

Transport

↓

Planner

↓

QueryPlan / MutationPlan

↓

Runtime

↓

SQL

↓

Database

↓

Materializer

↓

Response
```

Every stage performs one clearly defined task.

---

# Dependency Graph

The dependency graph is intentionally simple.

```
                 Foundation
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   Runtime           SQL      Mapping.Generators
      ▲                               │
      │                               │
      └───────────────┬───────────────┘
                      │
         Generated Runtime Components
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   GraphQL          gRPC          WebApi
```

Dependencies always point toward more stable layers.

---

# Dependency Inversion

Generated code should implement Foundation contracts.

For example:

```csharp
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort id);
}
```

implemented by:

```csharp
public sealed class GeneratedMetadataProvider
    : IMetadataProvider
{
}
```

Runtime depends only on the interface.

---

# Compile-Time Pipeline

Compilation proceeds through several stages:

```
Model Discovery

↓

Validation

↓

Relationship Resolution

↓

Identifier Allocation

↓

Metadata Construction

↓

Source Generation
```

Runtime never repeats these operations.

---

# Runtime Pipeline

Execution follows a predictable sequence:

```
Immutable Plan

↓

SQL Generation

↓

Database Execution

↓

Materialization

↓

Result
```

Execution should remain deterministic and allocation-conscious.

---

# Design Principles

The architecture is guided by several enduring principles:

- Compile-time over runtime
- Immutable metadata
- Immutable execution plans
- Dependency inversion
- Transport independence
- Explicit project boundaries
- Composition over inheritance
- Native AOT compatibility

These principles should remain stable as the framework evolves.

---

# Extensibility

CoffeeBeanery is extended through Foundation interfaces.

Key extension points include:

- `IMetadataProvider`
- `IPlannerRegistry`
- `ISqlDialect`
- `ISqlWriter`
- `IGraphStrategy`
- `IEntityMaterializer`
- `IEntityDematerializer`

New implementations integrate through Dependency Injection rather than modifying Runtime.

---

# Long-Term Direction

The architecture is intended to support:

- Multiple transports
- Multiple SQL dialects
- Graph databases
- Additional storage engines
- Compile-time tooling
- Native AOT deployment

without requiring changes to the core Runtime.

---

# Summary

CoffeeBeanery separates compile-time analysis from runtime execution.

The Generator discovers and generates.

Foundation defines contracts.

Runtime executes immutable plans.

SQL serializes those plans.

Transports translate external requests.

This layered architecture provides high performance, clear boundaries, strong testability, and long-term extensibility while keeping the Runtime simple and reusable.

# Runtime Architecture

> The Runtime is the execution engine of CoffeeBeanery. It consumes immutable execution plans and generated metadata to execute queries and mutations without performing runtime discovery or compile-time analysis.

The Runtime is intentionally small, deterministic, and transport-independent.

---

# Philosophy

The Runtime has one responsibility:

> Execute immutable plans.

It should never discover information.

It should never infer behavior.

It should simply execute.

---

# Responsibilities

The Runtime owns:

- Query execution
- Mutation execution
- Transaction coordination
- Materialization orchestration
- Generated value propagation
- Dependency execution
- Graph execution

The Runtime does **not** own:

- Metadata discovery
- SQL generation
- GraphQL
- Roslyn
- Reflection
- Source generation

---

# Project Layout

```
CoffeeBeanery.Runtime

Planner/

Execution/

Query/

Mutation/

Materialization/

Services/

Interceptors/

DependencyInjection/
```

Each namespace contains one logical responsibility.

---

# Runtime Pipeline

Every request follows the same execution pipeline.

```
Immutable Plan

↓

Execution Context

↓

SQL Generation

↓

Database Execution

↓

Materialization

↓

Return Result
```

The Runtime coordinates each stage but delegates specialized work to other layers.

---

# Query Execution

Query execution begins with a `QueryPlan`.

```
QueryPlan

↓

Runtime

↓

SQL

↓

Database

↓

Materializer

↓

Result
```

The Runtime never modifies the plan.

---

# Mutation Execution

Mutation execution begins with a `MutationPlan`.

```
MutationPlan

↓

Dependency Graph

↓

SQL

↓

Execution

↓

Generated Values

↓

Materialization
```

Dependency ordering has already been computed during planning.

---

# Execution Context

The execution context carries request-scoped state.

Typical contents include:

- Database connection
- Transaction
- SQL parameters
- Cancellation token
- Execution options

Execution contexts should remain lightweight.

---

# Dependency Graph Execution

Mutations frequently depend on previously generated values.

Example:

```
Customer

↓

CustomerAddress

↓

CustomerOrder
```

The planner computes dependency ordering.

Runtime executes operations in dependency order.

No dependency analysis occurs during execution.

---

# Materialization

Runtime coordinates generated materializers.

```
DbDataReader

↓

Generated Materializer

↓

CLR Object
```

Runtime does not inspect CLR properties.

Generated code performs object construction.

---

# Generated Values

Mutation execution often produces values required by later operations.

Example:

```
INSERT Customer

↓

Generated Id

↓

INSERT Address

↓

Generated AddressId

↓

INSERT Order
```

Runtime propagates values using the precomputed dependency graph.

---

# Transactions

Runtime coordinates transaction boundaries.

Typical workflow:

```
Begin Transaction

↓

Execute Plan

↓

Commit

↓

Return Result
```

Failures result in rollback.

Transaction policy remains transport-independent.

---

# Error Handling

Runtime reports execution failures through well-defined exception types.

Typical categories include:

- Validation
- Planning
- SQL
- Materialization
- Transaction
- Graph execution

Transport layers translate these exceptions into protocol-specific responses.

---

# Metadata Access

Runtime consumes metadata through interfaces.

Example:

```csharp
IMetadataProvider
```

Typical implementation:

```csharp
GeneratedMetadataProvider
```

Runtime should never reference generated static classes directly.

---

# Planner Registry

Generated planners are resolved through a planner registry.

```csharp
IPlannerRegistry
```

The Runtime requests planners without knowing how they were generated.

---

# Graph Execution

Graph operations are coordinated through graph strategies.

Example:

```csharp
IGraphStrategy
```

Implementations may target:

- Apache AGE
- PostgreSQL extensions
- Future graph providers

Runtime remains graph-provider independent.

---

# Dependency Injection

Runtime registers only execution services.

Example:

```csharp
services.AddSingleton<IQueryExecutor,
                      QueryExecutor>();

services.AddSingleton<IMutationExecutor,
                      MutationExecutor>();
```

Generated services are registered separately.

---

# Thread Safety

Runtime services should generally be stateless.

Immutable metadata and immutable execution plans naturally support concurrent execution.

Mutable state should remain confined to execution contexts.

---

# Performance

Runtime should avoid:

- Reflection
- Metadata discovery
- Dynamic dispatch where unnecessary
- Repeated allocations
- Runtime expression compilation

Execution should consist primarily of coordinating already-generated components.

---

# Native AOT

The Runtime is designed for Native AOT compatibility.

It avoids:

- Reflection
- Runtime code generation
- Dynamic proxy creation
- Runtime assembly scanning

Generated implementations replace these mechanisms.

---

# Future Evolution

Potential Runtime enhancements include:

- Streaming execution
- Batch execution
- Parallel dependency execution
- Distributed execution
- Pipeline instrumentation
- Advanced diagnostics

These features should preserve the Runtime's role as an execution coordinator rather than expanding its responsibilities.

---

# Summary

The Runtime is the heart of CoffeeBeanery's execution model.

It executes immutable plans, coordinates SQL generation, manages transactions, orchestrates generated materializers, and propagates generated values—all while remaining transport-independent, Native AOT friendly, and free from compile-time concerns.

Its simplicity is a direct result of moving analysis and generation into earlier architectural layers.

# SQL Architecture

> The SQL layer is responsible for converting immutable execution plans into executable SQL statements. It acts as a serialization layer between the Runtime and the database and intentionally contains no business logic or planning logic.

The SQL project should remain database-focused and transport-independent.

---

# Philosophy

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

# Responsibilities

The SQL project owns:

- SQL serialization
- SQL dialect abstraction
- Parameter generation
- Identifier quoting
- Graph SQL generation
- Result reading
- Database-specific syntax

The SQL project does **not** own:

- Metadata generation
- Planning
- Materialization
- GraphQL
- Dependency analysis

---

# Project Structure

```
CoffeeBeanery.Sql

Builders/

Dialects/

PostgreSql/

Readers/

Visitors/

Writers/

Extensions/
```

Each namespace represents one area of SQL generation.

---

# Execution Flow

The SQL layer receives immutable plans.

```
QueryPlan

↓

SqlWriter

↓

SQL Statement

↓

Database
```

or

```
MutationPlan

↓

SqlWriter

↓

SQL Statement

↓

Database
```

SQL generation should always be deterministic.

---

# SQL Writers

SQL writers serialize execution plans.

Typical responsibilities:

- SELECT
- INSERT
- UPDATE
- DELETE
- UPSERT
- RETURNING
- Common Table Expressions (CTEs)

Writers should remain declarative.

---

# SQL Readers

Readers convert raw database results into structures suitable for materialization.

Responsibilities include:

- DbDataReader helpers
- Typed value access
- Database-specific conversions

Readers should not construct application models.

---

# SQL Dialects

Database-specific syntax belongs to dialect implementations.

Example interface:

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteOffset(...);

    void WriteConflict(...);

    void WriteReturning(...);
}
```

Supported dialects may include:

- PostgreSQL
- SQL Server
- SQLite
- MySQL
- Oracle

---

# Builders

Builders generate reusable SQL fragments.

Examples:

```
SelectBuilder

InsertBuilder

UpdateBuilder

DeleteBuilder

WhereBuilder

OrderBuilder
```

Builders should compose statements rather than execute them.

---

# Visitors

Visitors traverse execution plans.

Example visitors:

```
ProjectionVisitor

JoinVisitor

FilterVisitor

OrderingVisitor

GraphVisitor
```

Visitors simplify SQL generation while keeping writers focused.

---

# Parameters

Parameters should always be generated separately from SQL text.

Example:

```
QueryPlan

↓

SQL

+

Parameter Collection

↓

DbCommand
```

Avoid embedding literal values unless explicitly required.

---

# Identifier Quoting

Identifier quoting belongs entirely to the SQL dialect.

Example:

PostgreSQL

```sql
"Customer"
```

SQL Server

```sql
[Customer]
```

Runtime should never know identifier syntax.

---

# Query Generation

Typical pipeline:

```
Projection

↓

FROM

↓

JOIN

↓

WHERE

↓

GROUP BY

↓

ORDER BY

↓

LIMIT

↓

OFFSET
```

Each clause should be generated independently.

---

# Mutation Generation

Mutation generation typically consists of:

```
INSERT

↓

ON CONFLICT

↓

DO UPDATE

↓

RETURNING
```

or

```
WITH

↓

Dependency CTEs

↓

INSERT

↓

RETURNING
```

Dependency ordering is supplied by the Runtime.

---

# Graph SQL

Graph operations should be isolated behind graph strategies.

Example:

```csharp
IGraphStrategy
```

Possible implementations:

- Apache AGE
- Future graph providers

Graph SQL should not leak into general SQL writers.

---

# Performance

SQL generation should:

- Minimize allocations
- Use StringBuilder
- Avoid repeated metadata lookups
- Avoid reflection
- Produce deterministic output

Generated SQL should be stable across executions.

---

# Error Handling

SQL writers should fail fast when execution plans are invalid.

Runtime is responsible for ensuring plans are valid before serialization.

The SQL layer should assume well-formed input.

---

# Dependency Direction

The SQL project depends on:

```
Foundation

Runtime
```

It should never depend on:

- GraphQL
- Roslyn
- Generated code

This keeps SQL reusable across transports.

---

# Native AOT

The SQL layer is naturally compatible with Native AOT because it relies on immutable execution plans rather than runtime discovery.

Avoid introducing:

- Reflection
- Dynamic SQL generation through expression trees
- Runtime model inspection

---

# Future Evolution

Future enhancements may include:

- Additional SQL dialects
- Query batching
- Prepared statement caching
- Bulk operations
- Vendor-specific optimizations
- SQL formatting tools
- Query diagnostics

These features should preserve the SQL layer's role as a serializer.

---

# Summary

The SQL project converts immutable execution plans into executable SQL while remaining independent of GraphQL, metadata generation, and application models.

By focusing solely on serialization, the SQL layer remains simple, deterministic, reusable, and easily extensible to additional database providers without impacting the rest of the CoffeeBeanery architecture.

# Generator Architecture

> The Mapping Generator is the compile-time engine of CoffeeBeanery. It analyzes application models, validates mappings, resolves relationships, and generates strongly typed runtime components that eliminate the need for runtime reflection.

The Generator is responsible for moving as much work as possible from execution time to compilation.

---

# Philosophy

The Generator has one responsibility:

> Analyze once during compilation so Runtime never has to analyze again.

Everything expensive should happen here.

Runtime should consume generated artifacts rather than discover application structure dynamically.

---

# Responsibilities

The Generator owns:

- Roslyn analysis
- Model discovery
- Attribute processing
- Validation
- Relationship resolution
- Identifier allocation
- Metadata generation
- Planner generation
- Materializer generation
- Runtime registration generation

The Generator does **not** own:

- Query execution
- SQL generation
- GraphQL execution
- Database access

---

# Project Structure

```
CoffeeBeanery.Mapping.Generators

Parser/

Model/

Passes/

Validation/

Emit/

Utilities/

Diagnostics/

MappingNodeGenerator.cs
```

Each namespace corresponds to a stage in the compilation pipeline.

---

# Compilation Pipeline

The Generator executes several deterministic stages.

```
Roslyn Compilation

↓

Parser

↓

Model Construction

↓

Validation

↓

Relationship Resolution

↓

Identifier Allocation

↓

Metadata Construction

↓

Code Generation
```

Every stage receives immutable input and produces immutable output.

---

# Parser

The Parser discovers annotated application models.

Typical responsibilities:

- Entity discovery
- Property discovery
- Relationship discovery
- Graph discovery
- Attribute parsing

The parser should not emit code.

---

# Validation

Validation ensures model consistency before generation begins.

Examples include:

- Duplicate entities
- Duplicate identifiers
- Invalid relationships
- Missing keys
- Unsupported property types
- Circular dependencies

Compilation should fail with meaningful diagnostics whenever possible.

---

# Internal Model

The Generator builds an internal representation of the application's structure.

Example objects include:

```
EntityNode

ModelNode

PropertyNode

RelationshipNode

GraphNode
```

Emitters consume these nodes rather than Roslyn symbols directly.

---

# Relationship Resolution

Relationships are resolved once during compilation.

Examples:

- One-to-one
- One-to-many
- Many-to-many
- Graph edges
- Lookup relationships

Runtime never repeats this work.

---

# Identifier Allocation

Stable identifiers are generated for:

- Entities
- Models
- Fields
- Columns
- Graphs
- Joins

Identifiers should remain deterministic across builds whenever possible.

---

# Metadata Construction

The Generator constructs immutable metadata objects.

Typical metadata includes:

```
EntityMetadata

ModelMetadata

ColumnMetadata

JoinMetadata

GraphMetadata
```

These objects are emitted into the generated metadata provider.

---

# Emitters

Each emitter has one responsibility.

Recommended layout:

```
Emit/

IdEmitter.cs

MetadataEmitter.cs

PlannerEmitter.cs

MaterializerEmitter.cs

DematerializerEmitter.cs

InterceptorEmitter.cs

RuntimeRegistryEmitter.cs

DependencyInjectionEmitter.cs
```

Small, focused emitters are easier to test and maintain.

---

# Generated Components

Compilation should produce components similar to:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedEntityIds

GeneratedServiceCollectionExtensions
```

Generated code should primarily contain immutable data and registrations.

---

# Dependency Direction

The Generator depends on:

```
Foundation

Roslyn
```

It should **never** depend on:

- Runtime
- SQL
- GraphQL
- gRPC
- Web API

Generated code targets Foundation contracts.

---

# Incremental Generation

The Generator should use Roslyn Incremental Generators.

Benefits include:

- Faster incremental builds
- Reduced memory usage
- Cached intermediate stages
- Deterministic execution
- Better IDE responsiveness

Each pipeline stage should invalidate only when its inputs change.

---

# Diagnostics

Diagnostics should clearly explain problems.

Good diagnostics include:

- Error code
- Short description
- Detailed explanation
- Suggested fix
- Source location

Developers should be able to resolve issues without inspecting generated code.

---

# Deterministic Output

Generation should always be deterministic.

Given identical source input, generated output should also be identical.

Deterministic generation simplifies:

- Snapshot testing
- Code reviews
- Debugging
- Continuous Integration

---

# Testing

The Generator should have dedicated tests for:

- Parsing
- Validation
- Relationship resolution
- Identifier allocation
- Metadata generation
- Snapshot generation
- Diagnostics

Each emitter should be independently testable.

---

# Performance

Generator performance matters because it runs during every compilation.

Guidelines include:

- Cache intermediate models
- Avoid repeated Roslyn traversal
- Minimize allocations
- Prefer immutable data
- Keep incremental inputs as small as possible

Fast generators improve the overall developer experience.

---

# Native AOT

Because all runtime discovery happens during compilation, the Generator enables Native AOT compatibility by producing static implementations that replace reflection and dynamic behavior.

Runtime simply executes generated code.

---

# Future Evolution

Potential future capabilities include:

- Roslyn analyzers
- Code fixes
- Query plan visualizers
- Metadata inspection tools
- SQL preview generation
- Build-time performance reports

These features build upon the same internal model and generation pipeline.

---

# Summary

The Mapping Generator is the compile-time intelligence behind CoffeeBeanery.

It analyzes application models, validates mappings, resolves relationships, generates immutable metadata, and produces the strongly typed runtime components that make the framework fast, deterministic, transport-independent, and fully compatible with Native AOT.

# Foundation Architecture

> Foundation is the architectural root of CoffeeBeanery. It defines the contracts, metadata models, identifiers, and planning primitives that every other project depends upon. Foundation intentionally contains no implementation logic beyond immutable value objects and shared abstractions.

Its purpose is to establish a stable vocabulary for the entire framework.

---

# Philosophy

Foundation answers one question:

> **What exists?**

It deliberately does **not** answer:

- How queries execute
- How SQL is generated
- How GraphQL works
- How metadata is discovered

Those responsibilities belong to higher layers.

---

# Responsibilities

Foundation owns:

- Metadata definitions
- Runtime contracts
- Planning primitives
- Identifier types
- Shared value objects
- Core abstractions

Foundation never owns:

- Runtime execution
- SQL generation
- Roslyn
- GraphQL
- Source generation
- Database providers

---

# Project Structure

```
CoffeeBeanery.Foundation

Metadata/

Interfaces/

Planning/

Ids/

Primitives/

Utilities/
```

Each namespace contains immutable contracts shared across the framework.

---

# Metadata

Metadata represents immutable facts about the application's structure.

Typical metadata objects include:

```
EntityMetadata

ModelMetadata

ColumnMetadata

JoinMetadata

GraphMetadata

FieldMetadata

MutationColumn

ColumnReference
```

Metadata is generated during compilation and consumed during execution.

---

# Interfaces

Foundation defines the contracts implemented by generated code and consumed by Runtime.

Examples include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer
```

Runtime depends only upon these abstractions.

---

# Planning

Planning primitives describe work that Runtime will execute.

Examples include:

```
QueryPlan

MutationPlan

Projection

Selection

JoinPlan

GraphPlan
```

Planning primitives are immutable.

---

# Identifiers

Foundation defines strongly typed identifiers for generated artifacts.

Typical identifiers include:

```
EntityId

StorageEntityId

ModelId

FieldId

ColumnId

GraphId
```

Identifiers should be deterministic and generated at compile time.

---

# Primitives

Primitives represent reusable framework concepts.

Examples:

```
SortDirection

FilterOperation

JoinType

RelationshipKind

MutationOperation
```

Primitives should remain stable over time.

---

# Dependency Direction

Foundation sits at the bottom of the dependency graph.

```
Foundation
      ▲
      │
Runtime
      ▲
      │
SQL
      ▲
      │
Generated Code
      ▲
      │
GraphQL
gRPC
WebApi
```

Foundation references no other CoffeeBeanery project.

---

# Immutability

Every metadata object should be immutable.

Example:

```csharp
public sealed class EntityMetadata
{
    public ushort Id { get; }

    public string Name { get; }

    public ImmutableArray<ColumnMetadata> Columns { get; }
}
```

Immutable objects:

- are thread-safe
- simplify caching
- improve predictability
- eliminate synchronization

---

# Dependency Inversion

Foundation defines interfaces rather than implementations.

Example:

```csharp
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort id);
}
```

implemented by generated code:

```csharp
GeneratedMetadataProvider
```

Foundation never references generated implementations.

---

# Runtime Independence

Foundation intentionally knows nothing about Runtime.

It should never reference:

- QueryExecutor
- MutationExecutor
- SQL writers
- Materializers
- GraphQL resolvers

This separation keeps contracts stable.

---

# SQL Independence

Foundation does not know SQL exists.

Metadata describes entities and relationships—not SQL syntax.

Identifier quoting, dialects, and serialization belong entirely to the SQL project.

---

# Transport Independence

Foundation has no knowledge of:

- GraphQL
- gRPC
- REST
- ASP.NET Core

Those projects simply consume Foundation contracts.

---

# Native AOT

Foundation naturally supports Native AOT because it contains:

- immutable objects
- interfaces
- value types
- compile-time metadata contracts

No reflection or runtime discovery should exist in Foundation.

---

# Versioning

Foundation should evolve slowly.

Breaking changes ripple throughout every dependent project.

Changes should prioritize:

- Backward compatibility
- Simplicity
- Stability
- Explicitness

Foundation is the most stable project in the solution.

---

# Future Evolution

Foundation may expand to include additional shared contracts, such as:

- Caching abstractions
- Diagnostics contracts
- Execution metrics
- Schema descriptors
- Provider capabilities

These additions should remain implementation-agnostic.

---

# Summary

Foundation establishes the architectural language of CoffeeBeanery.

It defines immutable metadata, planning primitives, identifiers, and interfaces while deliberately avoiding execution logic, SQL concerns, source generation, and transport-specific behavior.

Every other project builds upon Foundation, making it the stable base that enables dependency inversion, transport independence, Native AOT compatibility, and long-term architectural consistency.

# Dependency Injection

> CoffeeBeanery uses Dependency Injection (DI) to compose generated components with the Runtime. Dependency Injection enables dependency inversion, improves testability, and keeps generated code replaceable while preserving a clean architectural separation between compile-time and runtime concerns.

---

# Philosophy

Dependency Injection answers one question:

> **How are framework components composed?**

It should never determine:

- Query behavior
- SQL generation
- Planning logic
- Metadata construction

Those responsibilities belong elsewhere.

---

# Architectural Role

Dependency Injection sits at the composition root.

```
Application

↓

Dependency Injection

↓

Runtime

↓

Foundation Contracts

↓

Generated Implementations
```

Runtime depends only upon abstractions.

Applications decide which implementations to register.

---

# Composition Root

Each transport owns its own composition root.

Examples:

```
CoffeeBeanery.GraphQL

CoffeeBeanery.WebApi

CoffeeBeanery.Grpc
```

Each project registers Runtime plus generated services.

---

# Foundation Contracts

The Runtime depends on interfaces defined by Foundation.

Typical contracts include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer

ISqlDialect

IGraphStrategy
```

Generated implementations satisfy these interfaces.

---

# Generated Registration

The Generator should emit a registration extension.

Example:

```csharp
public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection
        AddGeneratedCoffeeBeanery(
            this IServiceCollection services)
    {
        services.AddSingleton<IMetadataProvider,
            GeneratedMetadataProvider>();

        services.AddSingleton<IPlannerRegistry,
            GeneratedPlannerRegistry>();

        return services;
    }
}
```

Generated code contains registrations—not application logic.

---

# Runtime Registration

Runtime exposes its own registration extension.

Example:

```csharp
public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection
        AddCoffeeBeaneryRuntime(
            this IServiceCollection services)
    {
        services.AddSingleton<IQueryExecutor,
            QueryExecutor>();

        services.AddSingleton<IMutationExecutor,
            MutationExecutor>();

        return services;
    }
}
```

Runtime never registers generated components.

---

# SQL Registration

SQL providers expose separate registration methods.

Example:

```csharp
services.AddPostgreSql();
```

Internally this registers:

- ISqlWriter
- ISqlReader
- ISqlDialect
- IGraphStrategy

Database providers remain modular.

---

# GraphQL Registration

GraphQL composes the complete framework.

Typical setup:

```csharp
services

    .AddCoffeeBeaneryRuntime()

    .AddGeneratedCoffeeBeanery()

    .AddPostgreSql()

    .AddCoffeeBeaneryGraphQL();
```

GraphQL becomes a thin adapter over Runtime.

---

# gRPC Registration

gRPC follows the same pattern.

```csharp
services

    .AddCoffeeBeaneryRuntime()

    .AddGeneratedCoffeeBeanery()

    .AddPostgreSql()

    .AddCoffeeBeaneryGrpc();
```

Only the transport changes.

---

# Web API Registration

Likewise for Web API.

```csharp
services

    .AddCoffeeBeaneryRuntime()

    .AddGeneratedCoffeeBeanery()

    .AddPostgreSql()

    .AddCoffeeBeaneryWebApi();
```

Execution remains identical.

---

# Lifetime Guidelines

Recommended service lifetimes:

| Component | Lifetime |
|-----------|----------|
| Metadata Provider | Singleton |
| Planner Registry | Singleton |
| SQL Dialect | Singleton |
| Graph Strategy | Singleton |
| Query Executor | Singleton |
| Mutation Executor | Singleton |
| Materializers | Singleton |
| Dematerializers | Singleton |

Execution state belongs in scoped execution contexts rather than service instances.

---

# Replacing Implementations

Applications may replace any generated implementation.

Example:

```csharp
services.Replace(

    ServiceDescriptor.Singleton<
        IMetadataProvider,
        CustomMetadataProvider>());
```

Runtime requires no modification.

---

# Testing

Dependency Injection makes testing straightforward.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
    TestMetadataProvider>();
```

Unit tests can replace:

- Metadata
- Planner registry
- SQL dialect
- Graph strategy

without changing Runtime.

---

# Avoid Service Location

Runtime should receive dependencies through constructors.

Preferred:

```csharp
public QueryExecutor(
    IMetadataProvider metadata,
    ISqlWriter writer)
{
}
```

Avoid resolving services directly from `IServiceProvider`.

Constructor injection makes dependencies explicit and easier to test.

---

# Generated Components

Generated classes should remain simple.

Typical generated registrations include:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedInterceptors
```

Each implementation fulfills a Foundation contract.

---

# Extensibility

Applications may introduce custom implementations for:

- Metadata providers
- SQL dialects
- Graph strategies
- Materializers
- Interceptors

The framework remains closed for modification but open for extension.

---

# Native AOT

Explicit registrations work naturally with Native AOT.

Avoid:

- Assembly scanning
- Reflection-based registration
- Runtime type discovery

Generated registration methods ensure all required services are known at compile time.

---

# Future Evolution

Future generated registrations may include:

- Provider capability registration
- Diagnostics services
- Metrics services
- Health checks
- Generated analyzers

The composition model should remain explicit and deterministic.

---

# Summary

Dependency Injection is the composition mechanism that connects CoffeeBeanery's compile-time generated components with its Runtime.

By registering generated implementations behind stable Foundation interfaces, the framework achieves dependency inversion, transport independence, testability, extensibility, and full Native AOT compatibility without introducing runtime discovery or hidden dependencies.

# Code Generation Pipeline

> The CoffeeBeanery Mapping Generator is organized as a deterministic compilation pipeline. Each stage has a single responsibility, consumes immutable input, and produces immutable output for the next stage.

This document describes every stage of that pipeline.

---

# Overview

The generator follows a linear transformation model.

```
C# Source

↓

Roslyn

↓

Parser

↓

Semantic Model

↓

Validation

↓

Relationship Resolution

↓

Identifier Allocation

↓

Metadata Construction

↓

Planner Construction

↓

Code Emitters

↓

Generated Source
```

No stage should skip another.

---

# Design Goals

The generation pipeline is designed to be:

- Deterministic
- Incremental
- Testable
- Immutable
- Parallelizable
- Easy to debug

Each stage should be independently testable.

---

# Stage 1 — Roslyn Discovery

The Incremental Generator begins by discovering candidate syntax nodes.

Typical candidates include:

- Classes
- Records
- Interfaces
- Attributes

Only relevant syntax proceeds to semantic analysis.

---

# Stage 2 — Semantic Analysis

Syntax is transformed into Roslyn symbols.

Examples include:

```
INamedTypeSymbol

IPropertySymbol

IMethodSymbol
```

The remainder of the pipeline should operate on semantic information rather than syntax trees.

---

# Stage 3 — Parsing

The parser converts Roslyn symbols into CoffeeBeanery's internal model.

Example objects:

```
EntityNode

ModelNode

PropertyNode

GraphNode

RelationshipNode
```

This separates framework concepts from Roslyn APIs.

---

# Stage 4 — Validation

Validation ensures the internal model is consistent.

Typical checks include:

- Duplicate entities
- Duplicate columns
- Duplicate identifiers
- Missing keys
- Unsupported types
- Invalid graph definitions
- Circular references

Compilation should stop if validation fails.

---

# Stage 5 — Relationship Resolution

Relationships are resolved once during compilation.

Examples include:

```
One-to-One

One-to-Many

Many-to-Many

Graph Edge

Lookup

Ownership
```

Resolved relationships become immutable metadata.

---

# Stage 6 — Identifier Allocation

Stable identifiers are assigned.

Examples:

```
EntityId

StorageEntityId

ModelId

FieldId

ColumnId

GraphId

JoinId
```

Identifier allocation should remain deterministic across builds whenever possible.

---

# Stage 7 — Metadata Construction

The resolved model becomes immutable metadata.

Generated metadata includes:

```
EntityMetadata

ModelMetadata

ColumnMetadata

JoinMetadata

GraphMetadata
```

Metadata becomes the Runtime's source of truth.

---

# Stage 8 — Planner Construction

Planning metadata is generated.

Examples include:

- Query planners
- Mutation planners
- Projection descriptors
- Join descriptors
- Graph descriptors

Planning should require no runtime analysis.

---

# Stage 9 — Materialization Generation

Materializers are generated.

Example:

```
DbDataReader

↓

Generated Materializer

↓

CLR Object
```

No reflection is required during execution.

---

# Stage 10 — Dematerialization Generation

Dematerializers generate mutation values.

Example:

```
CLR Object

↓

Generated Dematerializer

↓

Mutation Values
```

Again, runtime property inspection is unnecessary.

---

# Stage 11 — Registry Generation

Generated registries connect Runtime to generated components.

Typical outputs:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers
```

Registries implement Foundation interfaces.

---

# Stage 12 — Dependency Injection

The final stage generates registration code.

Example:

```csharp
services.AddGeneratedCoffeeBeanery();
```

Applications register generated services without knowing implementation details.

---

# Incremental Boundaries

Every stage should invalidate only when required.

Example:

```
Entity Change

↓

Entity Metadata

↓

Planner

↓

Materializer
```

Unrelated entities should not trigger full regeneration.

---

# Error Reporting

Errors should be reported as early as possible.

Prefer:

```
Parser Error

↓

Compilation Stops
```

rather than allowing invalid models to reach emitters.

Each diagnostic should include:

- Error code
- Description
- Source location
- Suggested fix

---

# Testing Strategy

Each stage should have dedicated tests.

Examples:

```
Parser Tests

Validation Tests

Relationship Tests

Identifier Tests

Metadata Tests

Emitter Tests

Snapshot Tests
```

Testing stages independently simplifies debugging.

---

# Performance

Generator performance should prioritize:

- Incremental execution
- Minimal allocations
- Cached intermediate models
- Limited Roslyn traversal
- Small invalidation scopes

Fast incremental builds improve the developer experience.

---

# Native AOT

The entire pipeline exists to eliminate runtime discovery.

Everything generated during compilation replaces runtime reflection and dynamic behavior, making Runtime naturally compatible with Native AOT.

---

# Summary

The CoffeeBeanery code generation pipeline transforms application models into immutable runtime artifacts through a series of deterministic compilation stages.

By separating parsing, validation, relationship resolution, metadata construction, planner generation, materializer generation, and registration into independent stages, the framework remains maintainable, testable, performant, and extensible while keeping Runtime focused solely on execution.