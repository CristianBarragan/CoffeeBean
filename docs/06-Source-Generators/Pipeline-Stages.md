[Home](../../README.md) → [Documentation](../README.md) → [Source Generators](README.md) → **Pipeline Stages**

# Pipeline Stages

## Contents

- [Overview](#overview)
- [Design Goals](#design-goals)
- [Stage 1 — Roslyn Discovery](#stage-1--roslyn-discovery)
- [Stage 2 — Semantic Analysis](#stage-2--semantic-analysis)
- [Stage 3 — Parsing](#stage-3--parsing)
- [Stage 4 — Validation](#stage-4--validation)
- [Stage 5 — Relationship Resolution](#stage-5--relationship-resolution)
- [Stage 6 — Identifier Allocation](#stage-6--identifier-allocation)
- [Stage 7 — Metadata Construction](#stage-7--metadata-construction)
- [Stage 8 — Planner Construction](#stage-8--planner-construction)
- [Stage 9 — Materialization Generation](#stage-9--materialization-generation)
- [Stage 10 — Dematerialization Generation](#stage-10--dematerialization-generation)
- [Stage 11 — Registry Generation](#stage-11--registry-generation)
- [Stage 12 — Dependency Injection](#stage-12--dependency-injection)
- [Incremental Boundaries](#incremental-boundaries)

---

> The Foundgine Mapping Generator is organized as a deterministic compilation pipeline. Each stage has a single responsibility, consumes immutable input, and produces immutable output for the next stage.

This document describes every stage of that pipeline.

---

## Overview

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

## Design Goals

The generation pipeline is designed to be:

- Deterministic
- Incremental
- Testable
- Immutable
- Parallelizable
- Easy to debug

Each stage should be independently testable.

---

## Stage 1 — Roslyn Discovery

The Incremental Generator begins by discovering candidate syntax nodes.

Typical candidates include:

- Classes
- Records
- Interfaces
- Attributes

Only relevant syntax proceeds to semantic analysis.

---

## Stage 2 — Semantic Analysis

Syntax is transformed into Roslyn symbols.

Examples include:

```
INamedTypeSymbol

IPropertySymbol

IMethodSymbol
```

The remainder of the pipeline should operate on semantic information rather than syntax trees.

---

## Stage 3 — Parsing

The parser converts Roslyn symbols into Foundgine's internal model.

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

## Stage 4 — Validation

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

## Stage 5 — Relationship Resolution

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

## Stage 6 — Identifier Allocation

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

## Stage 7 — Metadata Construction

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

## Stage 8 — Planner Construction

Planning metadata is generated.

Examples include:

- Query planners
- Mutation planners
- Projection descriptors
- Join descriptors
- Graph descriptors

Planning should require no runtime analysis.

---

## Stage 9 — Materialization Generation

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

## Stage 10 — Dematerialization Generation

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

## Stage 11 — Registry Generation

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

## Stage 12 — Dependency Injection

The final stage generates registration code.

Example:

```csharp
services.AddGeneratedCoffeeBeanery();
```

Applications register generated services without knowing implementation details.

---

## Incremental Boundaries

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

## Error Reporting

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

## Testing Strategy

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

## Performance

Generator performance should prioritize:

- Incremental execution
- Minimal allocations
- Cached intermediate models
- Limited Roslyn traversal
- Small invalidation scopes

Fast incremental builds improve the developer experience.

---

## Native AOT

The entire pipeline exists to eliminate runtime discovery.

Everything generated during compilation replaces runtime reflection and dynamic behavior, making Runtime naturally compatible with Native AOT.

---

## Summary

The Foundgine code generation pipeline transforms application models into immutable runtime artifacts through a series of deterministic compilation stages.

---

## Related Documentation

- [Mapping Generator](Mapping-Generator.md)
- [Diagnostics](Diagnostics.md)
- [Foundation → Metadata](../03-Foundation/Metadata.md)

---

← Previous: [Diagnostics](Diagnostics.md)  |  Next: [Dependency Injection](../07-Dependency-Injection/README.md) →
