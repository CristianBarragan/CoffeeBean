[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Dependency Graph**

# Dependency Graph

## Contents

- [Dependency Graph](#dependency-graph-1)
- [Dependency Rules](#dependency-rules)
- [Foundation Contracts in Practice](#foundation-contracts-in-practice)

---

## Dependency Graph

> **This page describes the actual current project graph**, not a future target —
> see [Layers](Layers.md) for the aspirational, further-out layout (additional
> transports, additional database providers, etc.) this one is a real snapshot of.
> Enforced by `tests/Foundgine.Tests/ArchitectureTests.cs`, which every
> `ProjectReference` in the solution must be listed against — that test, not this
> page, is the source of truth if the two ever disagree.

```
Foundgine.Abstractions
        ↓
Foundgine.Foundation
        ↓
Foundgine.Metadata
        ↓
 ┌───────────────┬───────────────┬───────────────────────┐
 │               │               │                        │
Foundgine.Builders  Foundgine.Planning  Foundgine.Execution.Contracts
 │               │               │                        ↑
 │               │               │                        │
 └───────────────┴───────────────┴────────────────────────┘
                      │                                    │
                 Graphgine ──────────────────────────────────┘
                      │
                      ↓
             Graphgine.HotChocolate

Foundgine.Providers ──→ Foundgine.Execution.Contracts

Graphgine.Postgres          (standalone — see note below)
Graphgine.SourceGenerators  (standalone — see note below)
Foundgine.Diagnostics ──→ Foundgine.Foundation
Foundgine.Reflection  ──→ Foundgine.Abstractions
Foundgine.Serialization ──→ Foundgine.Metadata
Graphgine.Analyzers          (standalone, placeholder)
Graphgine.AspNetCore ──→ Graphgine.HotChocolate
```

Dependencies always point toward more stable layers: `Foundgine.Abstractions` has
zero dependencies of its own, and everything else ultimately depends on it,
directly or transitively.

Two projects are intentionally disconnected from the rest of the graph today:

- **`Graphgine.Postgres`** — Npgsql-specific connection/transaction primitives
  (`UnitOfWork`, `UnitOfWorkContext`, `AgeConnectionFactory`) split out of
  `Graphgine` so the core engine has no hard package dependency on a specific
  database driver. Nothing references it yet; it exists so a real
  `Foundgine.Providers.SqlExecutionProvider` implementation has somewhere to
  get its connection/transaction management from once that stops being a
  stub. See `docs/MIGRATION.md`'s "Graphgine.Postgres split" for how it got
  here.
- **`Graphgine.SourceGenerators`** — a Roslyn incremental generator, built as
  `netstandard2.0`. A source generator can't take a normal `ProjectReference`
  and still run inside the *consumer's* compilation, so it has none; it emits
  code that references `Foundgine.Metadata` types (`EntityId`, `FieldId`,
  `ColumnId`, ...) by name instead. See `Emit/IdEmitter.cs`.

Circular project references should never be introduced.

---

## Dependency Rules

The following rules hold today (and are enforced by `ArchitectureTests.cs`):

| Project | Allowed Dependencies |
|---------|-----------------------|
| `Foundgine.Abstractions` | *(none)* |
| `Foundgine.Foundation` | `Foundgine.Abstractions` |
| `Foundgine.Diagnostics` | `Foundgine.Foundation` |
| `Foundgine.Reflection` | `Foundgine.Abstractions` |
| `Foundgine.Metadata` | `Foundgine.Foundation` |
| `Foundgine.Serialization` | `Foundgine.Metadata` |
| `Foundgine.Builders` | `Foundgine.Metadata` |
| `Foundgine.Planning` | `Foundgine.Metadata` |
| `Foundgine.Execution.Contracts` | `Foundgine.Metadata` |
| `Foundgine.Providers` | `Foundgine.Execution.Contracts` |
| `Graphgine` | `Foundgine.Builders`, `Foundgine.Planning`, `Foundgine.Foundation`, `Foundgine.Metadata`, `Foundgine.Diagnostics`, `Foundgine.Execution.Contracts` |
| `Graphgine.Postgres` | *(none)* |
| `Graphgine.HotChocolate` | `Graphgine` |
| `Graphgine.SourceGenerators` | *(none — netstandard2.0, emits by name; see above)* |
| `Graphgine.Analyzers` | *(none — placeholder)* |
| `Graphgine.AspNetCore` | `Graphgine.HotChocolate` |

Generated code (from `Graphgine.SourceGenerators`) depends only on
`Foundgine.Metadata` types by name and is consumed through
`GeneratedMetadataProvider`/`PlannerRegistry`, not a `ProjectReference`.

---

## Foundation Contracts in Practice

## Foundation Contracts

`Graphgine` depends on interfaces defined at the Foundgine layer, plus its own
`IPlannerRegistry` (Graphgine-specific, since a "planner" is a GraphQL-engine
concept, not a generic platform one).

Actual contracts include:

```csharp
Foundgine.Metadata.IMetadataProvider
Foundgine.Execution.Contracts.IExecutionProvider
Graphgine.Execution.IPlannerRegistry
```

Generated implementations (`GeneratedMetadataProvider`, `PlannerRegistry`)
satisfy `IMetadataProvider`/`IPlannerRegistry`; `Foundgine.Providers`'
`SqlExecutionProvider`/`GraphExecutionProvider`/`CacheExecutionProvider`
satisfy `IExecutionProvider` (currently stubs — see
[Persistence](../08-Persistence/README.md)).

---

## Related Documentation

- [Layers](Layers.md)
- [Foundation → Contracts](../03-Foundation/Contracts.md)
- [Dependency Injection](../07-Dependency-Injection/README.md)

---

← Previous: [Request Pipeline](Request-Pipeline.md)  |  Next: [Foundation](../03-Foundation/README.md) →
