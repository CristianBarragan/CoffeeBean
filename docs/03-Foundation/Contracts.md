[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Contracts**

# Contracts

## Contents

- [Interfaces](#interfaces)
- [Planning](#planning)
- [Identifiers](#identifiers)
- [Primitives](#primitives)

---

## Interfaces

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

## Planning

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

## Identifiers

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

## Primitives

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

---

## Related Documentation

- [Metadata](Metadata.md)
- [Components](Components.md)
- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)

---

← Previous: [Metadata](Metadata.md)  |  Next: [Components](Components.md) →
