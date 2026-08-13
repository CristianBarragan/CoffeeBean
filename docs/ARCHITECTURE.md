# Architecture

Foundgine is a **semantic execution layer**. Its central boundary is between structured application intent and physical execution.

## Canonical pipeline

```text
                         INTENT SOURCES
              GraphQL · JSON · AI · application code
                                │
                                ▼
                         Semantic Intent
                                │
                                ▼
                            Resolution
                                │
                                ▼
                           Authorization
                                │
                                ▼
                         Execution Plan
                                │
                   ┌────────────┼────────────┐
                   ▼            ▼            ▼
                  SQL                     InMemory
                   │                         │
                   └────────────┬────────────┘
                                ▼
                         Result + Evidence
```

The pipeline has six canonical concepts:

| Concept | Meaning |
|---|---|
| **Semantic Model** | What the application exposes |
| **Intent** | What the caller requests |
| **Authorization** | What the caller is allowed to do |
| **Execution Plan** | What Foundgine intends to execute |
| **Provider** | What physically executes the plan |
| **Evidence** | What happened and why |

These terms are deliberately kept stable. New features should fit one of these concepts rather than introduce another overlapping model.

## The fundamental boundary

```text
Intent adapters

GraphQL ─┐
JSON ────┤
AI ──────┤
Code ────┘
    │
    ▼
┌───────────────────────────────┐
│           Foundgine           │
│                               │
│ Semantic model                │
│ Intent                        │
│ Resolution                    │
│ Authorization                 │
│ Planning                      │
│ Execution contracts           │
│ Evidence                      │
└───────────────┬───────────────┘
                │
                ▼
       Physical execution

SQL / EF / REST / other providers
```

### Core dependency rule

> **Foundgine Core must never depend on the transport used to express intent or the provider used to execute it.**

Therefore the semantic/planning/execution core must not take dependencies on GraphQL, Hot Chocolate, SQL, EF Core, OpenAI, MCP, or another transport/provider implementation.

Adapters and providers depend on Foundgine contracts; the core does not depend on those adapters and providers.

## Semantics

The semantic model describes application-facing meaning: entities, fields, relationships, connections, and capabilities.

It is not a second ORM entity model. Storage metadata remains responsible for physical facts such as tables, columns, keys, and foreign keys.

A Foundgine connection represents a known semantic traversal between application-facing models. It should only exist when that application-facing connection provides meaning beyond merely reproducing a storage foreign key.

## Intent

Intent describes the requested operation independently of the transport that produced it.

For example:

```text
Read Customer
  ├── fields: id, name
  └── traverse: contracts
```

A GraphQL AST or JSON document is therefore an input representation, not the canonical semantic representation.

## Authorization

Authorization is part of planning and execution semantics, not merely a preliminary boolean check.

```text
Request
   ↓
Resolve
   ↓
Authorize
   ↓
Authorization constraints attached to plan
   ↓
Provider execution
```

This preserves authorization semantics across the provider boundary.

## Planning

Planning turns authorized semantic intent into a provider-independent execution plan.

The plan should describe logical operations rather than SQL syntax or provider-specific APIs.

The long-term semantic algebra is intentionally small:

```text
Read
Filter
Project
Traverse
Aggregate
Order
Page
Mutate
Bind
Return
```

Providers remain responsible for translating those operations into physical work.

## Execution

Execution coordinates the provider boundary and result materialization.

The repository contains two deliberately different execution strategies: `Foundgine.Sql` for SQL execution and `Foundgine.InMemory` for direct CLR-backed execution. The in-memory provider is intentionally limited and exists primarily to prove that the logical execution plan is not SQL in disguise.

Provider parity is not claimed: the in-memory provider supports only the subset covered by its tests. A future production provider should be judged by whether it can consume the same logical plan without changing the semantic core.

## AOT

AOT is used to make stable topology and metadata available at compile time:

```text
Application model + relationships + connections
                         ↓
                    AOT generator
                         ↓
                 generated metadata
                         ↓
                      runtime
```

This reduces the need for runtime reflection-heavy discovery and makes the semantic topology explicit and inspectable.

## AI boundary

AI is an intent source, not a core dependency.

```text
AI
 ↓ structured intent
Foundgine
 ↓ authorization + planning + execution
Provider
```

The core should not contain LLM clients, prompts, conversations, agent loops, model orchestration, or MCP implementation details.

## What Foundgine is not

Foundgine deliberately does not try to become:

- an ORM;
- a GraphQL server;
- an LLM or agent framework;
- an identity or authorization provider;
- a workflow engine;
- a database;
- a runtime entity-mapping framework.

Those systems can integrate with Foundgine at the boundaries.
