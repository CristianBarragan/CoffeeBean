[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Request Pipeline**

# Request Pipeline

## Contents

- [Overview](#overview)
- [Phase 1 — Compilation](#phase-1--compilation)
- [Runtime Begins](#runtime-begins)
- [Mutation Flow](#mutation-flow)
- [Responsibility Matrix](#responsibility-matrix)
- [Architectural Benefits](#architectural-benefits)

---

> This document follows a request from the moment an application calls Foundgine until the final object is returned. It explains which project is responsible for each stage and how compile-time generation and runtime execution interact.

---

## Overview

Foundgine is divided into two major phases:

```
Compile Time

↓

Generated Runtime Components

↓

Runtime Execution
```

Compile-time builds knowledge.

Runtime consumes knowledge.

---

## Phase 1 — Compilation

Compilation begins with application models.

```
Application

↓

Entity Classes

↓

Attributes

↓

Relationships
```

The application contains only business models.

No runtime metadata exists yet.

---

## Phase 2 — Roslyn

The Incremental Generator receives the Roslyn compilation.

```
C# Source

↓

Roslyn

↓

Symbols
```

Roslyn exposes:

- Types
- Properties
- Attributes
- Generic information

---

## Phase 3 — Parsing

Foundgine parses Roslyn symbols.

```
Roslyn Symbols

↓

EntityNode

↓

ModelNode

↓

RelationshipNode
```

Roslyn APIs disappear after this stage.

The remaining pipeline uses Foundgine's internal model.

---

## Phase 4 — Validation

Validation verifies correctness.

Examples:

```
Duplicate Columns

↓

Error
```

```
Missing Key

↓

Error
```

```
Invalid Relationship

↓

Error
```

Compilation stops immediately when validation fails.

---

## Phase 5 — Relationship Resolution

Relationships become explicit.

```
Customer

↓

Orders

↓

OrderLines
```

becomes immutable metadata.

Runtime never analyzes relationships again.

---

## Phase 6 — Identifier Allocation

Stable identifiers are assigned.

```
Customer

↓

EntityId = 0
```

```
Order

↓

EntityId = 1
```

These identifiers become array indexes throughout Runtime.

---

## Phase 7 — Metadata Construction

Metadata objects are built.

```
EntityNode

↓

EntityMetadata
```

```
RelationshipNode

↓

JoinMetadata
```

Metadata is immutable.

---

## Phase 8 — Source Generation

The Generator emits code.

Examples:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedEntityIds
```

Compilation finishes.

---

## Runtime Begins

Application startup registers generated components.

```
services

↓

AddGeneratedCoffeeBeanery()

↓

Dependency Injection
```

Runtime is now fully configured.

---

## Incoming Request

A request may originate from:

```
GraphQL

gRPC

REST

CLI

Background Service
```

Transport does not affect Runtime.

---

## Planner

The transport asks a planner to build a plan.

```
Request

↓

Planner

↓

QueryPlan
```

or

```
MutationPlan
```

Plans are immutable.

---

## Runtime

Runtime receives the plan.

```
QueryPlan

↓

Runtime
```

Runtime does not inspect CLR models.

Runtime does not discover metadata.

Runtime executes only.

---

## Metadata Lookup

Runtime requests metadata.

```
IMetadataProvider

↓

GeneratedMetadataProvider

↓

EntityMetadata
```

Metadata lookup is deterministic.

---

## SQL

Runtime delegates SQL generation.

```
QueryPlan

↓

SqlWriter

↓

SQL
```

SQL generation performs serialization only.

---

## Database

The SQL statement executes.

```
SQL

↓

Database

↓

Rows
```

The Runtime does not know database syntax.

---

## Materialization

Generated materializers convert rows into CLR objects.

```
Rows

↓

Generated Materializer

↓

Customer
```

No reflection occurs.

---

## Response

The transport receives the final object.

```
Runtime

↓

GraphQL

↓

JSON
```

or

```
Runtime

↓

gRPC

↓

Protobuf
```

Execution is complete.

---

## Mutation Flow

Mutation execution follows a similar pipeline.

```
Mutation

↓

Planner

↓

MutationPlan

↓

Dependency Graph

↓

SQL

↓

Generated Values

↓

Materialization

↓

Response
```

Dependency ordering has already been computed during planning.

---

## Responsibility Matrix

| Stage | Project |
|---------|----------|
| Model Discovery | Mapping.Generators |
| Validation | Mapping.Generators |
| Metadata Construction | Mapping.Generators |
| Planner Generation | Mapping.Generators |
| Metadata Contracts | Foundation |
| Runtime Execution | Runtime |
| SQL Serialization | Sql |
| Transport | GraphQL / gRPC / WebApi |

Each project owns exactly one concern.

---

## Architectural Benefits

This separation provides several advantages:

- Compile-time analysis
- Deterministic Runtime
- Native AOT compatibility
- Transport independence
- Database abstraction
- Clear dependency direction
- Excellent testability

Each layer remains focused on its own responsibility.

---

## Summary

Foundgine performs all expensive analysis during compilation, generating immutable runtime artifacts that are consumed by a lightweight execution engine.

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Source Generators → Pipeline Stages](../06-Source-Generators/Pipeline-Stages.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Layers](Layers.md)  |  Next: [Dependency Graph](Dependency-Graph.md) →
