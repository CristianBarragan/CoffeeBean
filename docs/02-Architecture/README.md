# Architecture

[Home](../../README.md) → [Documentation](../README.md) → **Architecture**

Foundgine is built around a strict separation between:

```text
meaning
planning
execution
transport
```

## Current architecture

```text
                Application / AI
                       │
                       ▼
               Foundgine.Semantic
                       │
                       ▼
             Structured Intent / Resolve
                       │
                       ▼
               Foundgine.Planning
                       │
                       ▼
               Foundgine.Builders
                       │
                       ▼
          Foundgine.Execution.Contracts
                       │
                       ▼
              Foundgine.Providers
                       │
                       ▼
                  Database
```

The semantic and planning projects are deliberately separate. The semantic layer translates structured meaning into the existing planning vocabulary; it does not replace the planner.

## Active projects

| Project | Responsibility |
|---|---|
| `Foundgine.Abstractions` | Lowest-level reusable contracts |
| `Foundgine.Foundation` | Domain-neutral primitives |
| `Foundgine.Metadata` | Entity/storage/relationship metadata |
| `Foundgine.Diagnostics` | Diagnostics support |
| `Foundgine.Builders` | Provider-neutral logical plan structures |
| `Foundgine.Semantic` | Application-domain meaning, inference and resolution |
| `Foundgine.Planning` | Dynamic logical planning |
| `Foundgine.Execution.Contracts` | Provider-neutral execution contracts |
| `Foundgine.Providers` | SQL provider compilation/execution |

## Critical rules

No lower layer should know about a higher-level concern.

```text
Metadata  ✗ SQL
Metadata  ✗ AI
Planning  ✗ LLM
Planning  ✗ GraphQL
Execution.Contracts ✗ provider implementation
Semantic ✗ SQL
```

Adapters may connect the layers.

## Semantic versus planning

`Foundgine.Semantic` answers:

> **What does the caller mean?**

`Foundgine.Planning` answers:

> **How can that structured request be executed?**

Resolution is identity-oriented:

```text
"Ada Lovelace" → Customer #1
```

Traversal is set-oriented:

```text
Customer #1 → Accounts* → Transactions*
```

This distinction prevents collection-valued relationships from being incorrectly collapsed into a single identity.

## Why Semantic is separate

The semantic layer must remain provider-neutral. It can describe a rich logical/domain model without knowing whether the eventual execution is SQL, retrieval, an external system or another provider.

The current work is to make the semantic → `QueryIntent` bridge explicit and reusable without creating a second planner.
