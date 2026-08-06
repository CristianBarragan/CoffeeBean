[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Metadata**

# Metadata

## Contents

- [Metadata](#metadata-1)
- [Dependency Direction](#dependency-direction)
- [Immutability](#immutability)

---

## Metadata

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

## Dependency Direction

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

## Immutability

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

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Source Generators → Pipeline Stages](../06-Source-Generators/Pipeline-Stages.md)
- [Reference → Glossary](../13-Reference/Glossary.md)

---

← Previous: [Foundation](README.md)  |  Next: [Contracts](Contracts.md) →
