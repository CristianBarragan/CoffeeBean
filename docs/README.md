# Foundgine Documentation

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

This documentation describes the **current active Foundgine architecture**, not the historical GraphQL/Graphgine implementation.

## Start here

1. [Product direction](00-Direction/README.md)
2. [Proof milestones](00-Direction/Milestones.md)
3. [Current status](CURRENT-STATUS.md)
4. [Architecture](02-Architecture/README.md)
5. [Banking proof](11-Samples/README.md)

## Core documentation

| Area | Purpose |
|---|---|
| [Getting Started](01-Getting-Started/README.md) | Build and run the repository |
| [Architecture](02-Architecture/README.md) | Dependency and execution boundaries |
| [Foundation](03-Foundation/README.md) | Stable domain-neutral contracts and metadata |
| [Runtime](04-Runtime/README.md) | Query/mutation planning and execution |
| [GraphQL](05-GraphQL/README.md) | Historical adapter context; not current product scope |
| [Source Generators](06-Source-Generators/README.md) | Historical generator and future Roslyn direction |
| [Dependency Injection](07-Dependency-Injection/README.md) | Composition guidance |
| [Persistence](08-Persistence/README.md) | Provider/execution boundary |
| [AI](09-AI/README.md) | Semantic model, intent and AI boundary |
| [Performance](10-Performance/README.md) | Benchmark plan and performance principles |
| [Samples](11-Samples/README.md) | Canonical E2E proof |
| [Contributing](12-Contributing/README.md) | Engineering rules and tests |
| [Reference](13-Reference/README.md) | Glossary, ADRs, FAQ and roadmap |

## Source of truth

When documents disagree, use this order:

1. active source and tests;
2. `docs/CURRENT-STATUS.md`;
3. `docs/00-Direction/Milestones.md`;
4. this documentation index;
5. `llms-full.md`;
6. historical/archive material.

`archive/` is not part of the active product proof.

## Accuracy rule

A capability may be:

- **modelled** — a contract/type exists;
- **implemented** — runtime code exists;
- **unit-proven** — tests cover it;
- **E2E-proven** — a real path executes against a real dependency;
- **productized** — the capability is exposed through a stable reusable runtime boundary.

Documentation must distinguish those states.
