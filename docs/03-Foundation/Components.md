[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Components**

# Components

## Contents

- [Responsibilities](#responsibilities)
- [Project Structure](#project-structure)
- [Runtime Independence](#runtime-independence)
- [SQL Independence](#sql-independence)
- [Transport Independence](#transport-independence)
- [Native AOT](#native-aot)
- [Versioning](#versioning)

---

## Responsibilities

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

## Project Structure

```
Foundgine.Foundation

Metadata/

Interfaces/

Planning/

Ids/

Primitives/

Utilities/
```

Each namespace contains immutable contracts shared across the framework.

---

## Runtime Independence

Foundation intentionally knows nothing about Runtime.

It should never reference:

- QueryExecutor
- MutationExecutor
- SQL writers
- Materializers
- GraphQL resolvers

This separation keeps contracts stable.

---

## SQL Independence

Foundation does not know SQL exists.

Metadata describes entities and relationships—not SQL syntax.

Identifier quoting, dialects, and serialization belong entirely to the SQL project.

---

## Transport Independence

Foundation has no knowledge of:

- GraphQL
- gRPC
- REST
- ASP.NET Core

Those projects simply consume Foundation contracts.

---

## Native AOT

Foundation naturally supports Native AOT because it contains:

- immutable objects
- interfaces
- value types
- compile-time metadata contracts

No reflection or runtime discovery should exist in Foundation.

---

## Versioning

Foundation should evolve slowly.

Breaking changes ripple throughout every dependent project.

Changes should prioritize:

- Backward compatibility
- Simplicity
- Stability
- Explicitness

Foundation is the most stable project in the solution.

---

---

## Related Documentation

- [Contracts](Contracts.md)
- [Architecture → Layers](../02-Architecture/Layers.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [Contracts](Contracts.md)  |  Next: [Extensibility](Extensibility.md) →
