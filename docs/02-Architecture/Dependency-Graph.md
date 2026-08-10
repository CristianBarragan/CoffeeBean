# Dependency Graph

[Home](../../README.md) → [Architecture](README.md) → **Dependency Graph**

The active project references are intentionally small.

```text
Foundgine.Abstractions
        ↑
Foundgine.Foundation
        ↑
Foundgine.Metadata
        ├──────────────┐
        │              │
        ▼              ▼
Foundgine.Builders   Foundgine.Semantic
        │
        ▼
Foundgine.Planning
        │
        ▼
Foundgine.Execution.Contracts
        ▲
        │
Foundgine.Providers
```

More precisely:

```text
Foundation → Abstractions
Metadata → Foundation
Diagnostics → Foundation
Builders → Metadata
Execution.Contracts → Metadata
Semantic → Metadata
Planning → Metadata + Builders
Providers → Builders + Execution.Contracts
```

## Why this matters

The dependency graph prevents accidental architectural erosion.

For example:

- semantic code cannot acquire SQL dependencies accidentally;
- planning cannot depend on GraphQL;
- execution contracts cannot depend on SQLite;
- providers remain replaceable.

## Tests

`Foundgine.Tests/ArchitectureTests.cs` machine-checks project-reference rules.

A dependency-direction change should therefore be treated as an architectural change, not a convenience refactor.
