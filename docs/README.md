# Foundgine Documentation

**Foundgine** is the reusable execution platform. **Graphgine** is the first product built on it.

This documentation describes the target architecture, current implementation and roadmap. Where
the architecture is ahead of the implementation, the page says so explicitly.

> **Status:** active architectural development. Some providers, hosting/analyzer projects, graph
> operations, tests and the Banking sample are incomplete. Do not treat every documented target
> as production-ready behavior.

## Start here

- [Repository README](../README.md)
- [Getting Started](01-Getting-Started/README.md)
- [Architecture](02-Architecture/README.md)
- [Foundation](03-Foundation/README.md)
- [Runtime](04-Runtime/README.md)
- [GraphQL / Graphgine](05-GraphQL/README.md)
- [Source Generators](06-Source-Generators/README.md)
- [Dependency Injection](07-Dependency-Injection/README.md)
- [Persistence](08-Persistence/README.md)
- [AI & LLM Readiness](09-AI/README.md)
- [Performance](10-Performance/README.md)
- [Samples](11-Samples/README.md)
- [Contributing](12-Contributing/README.md)
- [Reference](13-Reference/README.md)

## Architecture at a glance

```text
Application / Domain
        ↓
     Graphgine
        ↓
Foundgine.Planning / Foundgine.Builders
        ↓
Foundgine.Execution.Contracts / Foundgine.Providers
        ↓
Foundgine.Metadata
        ↓
Foundgine.Foundation
        ↓
Foundgine.Abstractions
```

`Graphgine.HotChocolate` is the GraphQL server integration boundary. Foundgine itself must not
depend upward on Graphgine or Hot Chocolate.

## Repository identity

| Name | Meaning |
|---|---|
| **Foundgine** | Reusable execution platform |
| **Graphgine** | First GraphQL-oriented product |
| **Foundgine** | Historical predecessor / legacy implementation |

The `legacy/` tree is retained for migration/reference purposes and is not the target architecture.

## Current implementation reality

Substantial implementation exists in the platform, Graphgine core, source generator and Hot
Chocolate adapter. Current gaps include incomplete provider paths, graph strategy work, placeholder
projects, placeholder tests and sample wiring.

For the authoritative AI-oriented description, see the repository root `llms-full.md`.
