[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Glossary**

# Glossary

## Contents

- [Entity](#entity)
- [Model](#model)
- [Storage Entity](#storage-entity)
- [Field](#field)
- [Column](#column)
- [Column Reference](#column-reference)
- [Metadata](#metadata)
- [Metadata Provider](#metadata-provider)
- [Query](#query)
- [Mutation](#mutation)
- [Query Planner](#query-planner)
- [Mutation Planner](#mutation-planner)
- [Query Plan](#query-plan)
- [Mutation Plan](#mutation-plan)
- [Materializer](#materializer)
- [Dematerializer](#dematerializer)
- [Planner Registry](#planner-registry)
- [Graph](#graph)
- [Join](#join)
- [Graph Strategy](#graph-strategy)
- [SQL Writer](#sql-writer)
- [SQL Dialect](#sql-dialect)
- [Generator](#generator)
- [Generated Code](#generated-code)
- [Foundation](#foundation)
- [Runtime](#runtime)
- [SQL Layer](#sql-layer)
- [Transport](#transport)
- [Dependency Inversion](#dependency-inversion)
- [Native AOT](#native-aot)
- [Summary](#summary)

---

> This glossary defines the core terminology used throughout the CoffeeBeanery framework. The terms below have specific architectural meanings and should be used consistently across documentation, code, and discussions.

---

## Entity

A physical storage object.

An Entity represents the database structure rather than the public application model.

Examples include:

- Customer
- Order
- Product

Entities map directly to storage metadata.

---

## Model

A public CLR representation exposed to the application.

A model may map to:

- one entity
- multiple entities
- graph traversals
- projections

Models are transport-independent.

---

## Storage Entity

The physical table or storage object used by SQL generation.

Every storage entity has:

- StorageEntityId
- EntityMetadata
- Columns
- Keys

---

## Field

A logical member exposed by a model.

Fields may represent:

- database columns
- computed values
- graph properties
- projections
- aggregates

Fields are not always physical columns.

---

## Column

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

## Column Reference

A lightweight object describing a physical column.

Typically includes:

- Entity
- Column Metadata
- Ordinal
- Identifier

Column references eliminate repeated metadata lookups.

---

## Metadata

Immutable information describing the application's data model.

Examples include:

- EntityMetadata
- ModelMetadata
- JoinMetadata
- GraphMetadata
- ColumnMetadata

Metadata is generated during compilation.

---

## Metadata Provider

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

## Query

A read operation.

Queries produce immutable QueryPlans which Runtime executes.

---

## Mutation

A write operation.

Mutations produce immutable MutationPlans containing dependency information and SQL operations.

---

## Query Planner

Converts selections into immutable QueryPlans.

Responsibilities include:

- projection planning
- join planning
- graph planning
- metadata resolution

---

## Mutation Planner

Converts mutation requests into immutable MutationPlans.

Responsibilities include:

- dependency analysis
- relationship resolution
- ordering
- generated value propagation

---

## Query Plan

An immutable description of a read operation.

Contains everything Runtime requires for execution.

---

## Mutation Plan

An immutable description of a write operation.

Includes:

- operations
- dependencies
- projections
- graph merges

---

## Materializer

Generated code that converts database rows into CLR objects.

Materializers replace reflection.

---

## Dematerializer

Generated code that converts CLR objects into mutation values.

Dematerializers replace runtime property inspection.

---

## Planner Registry

A generated registry that maps identifiers to planners.

Runtime uses the registry to locate generated planners without reflection.

---

## Graph

A graph representation consisting of:

- vertices
- edges
- traversals

Graph metadata exists independently of relational metadata.

---

## Join

A relationship between two storage entities.

Joins are resolved during planning rather than SQL generation.

---

## Graph Strategy

A provider responsible for graph-specific SQL generation.

Example implementations:

- Apache AGE
- Custom graph providers

---

## SQL Writer

A component that converts immutable execution plans into executable SQL statements.

SQL Writers do not perform planning.

---

## SQL Dialect

Defines database-specific SQL syntax.

Examples include:

- PostgreSQL
- SQL Server
- SQLite

Only serialization changes between dialects.

---

## Generator

The Roslyn Incremental Source Generator responsible for compile-time analysis and code generation.

The Generator produces:

- metadata
- identifiers
- planners
- materializers
- registrations

---

## Generated Code

Source emitted during compilation.

Generated code should contain:

- immutable metadata
- registrations
- strongly typed implementations

Generated code should contain very little business logic.

---

## Foundation

The lowest architectural layer.

Defines:

- contracts
- metadata
- identifiers
- planning primitives

Foundation references no other CoffeeBeanery project.

---

## Runtime

The execution engine.

Consumes:

- metadata
- planners
- SQL writers
- materializers

Runtime performs execution only.

---

## SQL Layer

The serialization layer responsible for converting execution plans into SQL.

It does not understand GraphQL or CLR models.

---

## Transport

An adapter that translates external requests into Runtime plans.

Examples include:

- GraphQL
- gRPC
- REST

Transports do not execute queries.

---

## Dependency Inversion

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

## Native AOT

Ahead-of-Time compilation supported through compile-time generation and the avoidance of runtime reflection.

CoffeeBeanery is designed to remain fully compatible with Native AOT.

---

## Summary

Using this terminology consistently helps maintain a shared architectural language across the CoffeeBeanery codebase. Contributors should prefer these definitions when naming types, writing documentation, reviewing pull requests, and discussing future design decisions.

---

## Related Documentation

- [FAQ](FAQ.md)
- [Architecture](../02-Architecture/README.md)
- [Foundation](../03-Foundation/README.md)

---

← Previous: [FAQ](FAQ.md)  |  Next: [Roadmap](Roadmap.md) →
