[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **Code Style**

# Code Style

## Contents

- [General Principles](#general-principles)
- [Architecture First](#architecture-first)
- [File Organization](#file-organization)
- [Naming](#naming)
- [Method Size](#method-size)
- [Immutability](#immutability)
- [Exceptions & Pattern Matching](#exceptions--pattern-matching)
- [Comments](#comments)

---

## General Principles

Code should be:

- Readable
- Predictable
- Deterministic
- Testable
- Allocation-conscious
- Native AOT friendly

When faced with two implementations of equal performance, always choose the simpler one.

---

## Architecture First

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

## File Organization

One public type per file.

Example:

```
EntityMetadata.cs

QueryPlanner.cs

GeneratedMetadataProvider.cs
```

Avoid grouping unrelated public types in the same file.

---

## Naming

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

## Method Size

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

## Immutability

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

## Exceptions

Throw exceptions only for exceptional situations.

Validation errors should occur during planning or generation whenever possible.

Runtime should rarely encounter invalid metadata.

---

## Comments

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

---

## Related Documentation

- [Contributing](README.md)
- [Architecture → Principles](../02-Architecture/Principles.md)
- [Testing](Testing.md)

---

← Previous: [Contributing](README.md)  |  Next: [Testing](Testing.md) →
