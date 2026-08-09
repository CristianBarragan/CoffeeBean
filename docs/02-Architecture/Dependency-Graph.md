[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Dependency Graph**

# Dependency Graph

The active solution deliberately keeps the platform small.

## Active graph

```text
Foundgine.Abstractions
        ↓
Foundgine.Foundation
        ↓
Foundgine.Metadata
        ↓
Foundgine.Builders
        ↓
Foundgine.Planning
        ↓
Foundgine.Execution.Contracts
        ↓
Foundgine.Providers
```

The exact `.csproj` references are the authoritative dependency graph.

## Responsibilities

| Project | Role |
|---|---|
| `Foundgine.Abstractions` | stable contracts |
| `Foundgine.Foundation` | generic primitives |
| `Foundgine.Metadata` | domain metadata |
| `Foundgine.Diagnostics` | diagnostics |
| `Foundgine.Builders` | logical plans |
| `Foundgine.Planning` | dynamic planning |
| `Foundgine.Execution.Contracts` | provider execution contracts |
| `Foundgine.Providers` | provider implementations |

## Rules

- No dependency cycles.
- Inner platform layers do not reference transports.
- Core projects do not reference LLM SDKs.
- Core projects do not reference MCP.
- Core projects do not reference GraphQL/Hot Chocolate.
- Database-specific code belongs behind provider boundaries.
- New integrations should be adapters.

## Historical projects

The former Graphgine/GraphQL projects are no longer in the active solution.

Historical source remains under `archive/`.

## Why this matters

The architecture is intended to allow:

```text
MCP
GraphQL
REST
gRPC
CLI
background jobs
```

to become clients of the same semantic/execution core without forcing their protocols into Foundgine's lowest layers.
