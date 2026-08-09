[Home](../../README.md) → [Documentation](../README.md) → **Architecture**

# Architecture

Foundgine is an application-domain semantic and execution layer for AI-native .NET applications.

The architecture has two complementary halves:

1. **Compile-time knowledge** — describe the application's legal domain vocabulary.
2. **Runtime execution** — resolve dynamic intent into safe, executable plans.

## Core architecture

```text
             APPLICATION DOMAIN
                     │
                     ▼
            Semantic Domain Model
                     │
          ┌──────────┼──────────┐
          │          │          │
      Entities   Relationships Actions
          │          │          │
          └──────────┼──────────┘
                     ▼
               Foundgine API
                     │
                     ▼
                  Intent
                     │
                     ▼
                Resolution
                     │
                     ▼
             Policy / Authorization
                     │
                     ▼
              Execution Plan
                     │
          ┌──────────┼──────────┐
          ▼          ▼          ▼
      Structured   Domain    External
         data      actions     data
          │          │
          └──────────┼──────────┘
                     ▼
                  Execute
                     │
                     ▼
                 Verify
                     │
                     ▼
                 Evidence
```

## Dependency architecture

The active platform is currently split into:

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

These boundaries are implementation boundaries, not a requirement that every future feature must become a new project.

## Key rule

The core platform must not depend on:

- a particular LLM
- MCP
- GraphQL
- Hot Chocolate
- a particular database
- a particular workflow engine

Those technologies integrate from the outside.

## Runtime principle

Runtime should consume explicit semantic and execution structures.

It should not repeatedly rediscover:

- CLR metadata
- domain relationships
- action legality
- provider capabilities

## Dynamic versus compiled knowledge

Compiled:

```text
What entities exist?
What relationships exist?
What actions are legal?
What policies apply?
```

Dynamic:

```text
What did the user mean?
Which customer did they mean?
Which transaction is "the last one"?
What execution plan satisfies the intent?
```

This distinction is central.

## Current proof

The Banking sample currently proves only the lower execution half:

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ SQLite
→ Result
```

The semantic/AI layers are the next milestone.

## Architecture documents

- [Direction](../00-Direction/README.md)
- [Milestones](../00-Direction/Milestones.md)
- [Layers](Layers.md)
- [Dependency Graph](Dependency-Graph.md)
- [Request Pipeline](Request-Pipeline.md)
- [Principles](Principles.md)
- [Current Status](../CURRENT-STATUS.md)
