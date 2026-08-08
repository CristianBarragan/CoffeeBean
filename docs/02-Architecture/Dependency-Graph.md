[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Dependency Graph**

# Dependency Graph

## Contents

- [Dependency Graph](#dependency-graph-1)
- [Dependency Rules](#dependency-rules)
- [Foundation Contracts in Practice](#foundation-contracts-in-practice)

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

## Foundation Contracts in Practice

## Foundation Contracts

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

---

## Related Documentation

- [Layers](Layers.md)
- [Foundation → Contracts](../03-Foundation/Contracts.md)
- [Dependency Injection](../07-Dependency-Injection/README.md)

---

← Previous: [Request Pipeline](Request-Pipeline.md)  |  Next: [Foundation](../03-Foundation/README.md) →
