# Architecture

Foundgine separates **semantic intent** from **physical execution**. Its core purpose is not to replace an ORM; it is to make the application's data-operation graph explicit and compilable.

```text
Input
  ↓
Semantic Intent
  ↓
Known Model / Connection Graph
  ↓
Resolve → Authorize → Plan
  ↓
Provider Plan
  ↓
EF / SQL / other provider
```

## The fundamental boundary

```text
EF / storage model
    │
    │ schema, keys, foreign keys, relationships
    ▼
Storage metadata

Domain model
    │
    │ semantic connections
    ▼
Foundgine graph
    │
    │ requested traversal
    ▼
Execution plan
```

EF remains responsible for database entities and their relational configuration. Foundgine does not create a second entity model and does not populate entities or domain models.

A Foundgine **connection** means:

> this model can communicate with / visit this known target.

The connection is static knowledge generated at build time. The request chooses which known connections to traverse.

## Boundaries

**Semantics** knows application meaning and the request graph. It does not know SQL.

**Metadata** contains stable storage and semantic topology facts.

**Planning** turns an authorized semantic traversal into logical operations. It does not need to rediscover relationships.

**Execution** owns the provider boundary.

**Providers** turn logical plans into physical work.

**Adapters** translate external protocols such as GraphQL into Foundgine intent.

## AOT

AOT is the preferred path for stable topology:

```text
entities + EF relationships + models + connections
                         ↓
                    AOT compiler
                         ↓
                 generated graph/metadata
                         ↓
                      runtime
```

Runtime should not perform reflection-heavy entity discovery, relationship inference, or object mapping.

## What Foundgine is not

Foundgine deliberately does not try to become:

- an AutoMapper replacement;
- a second EF entity configuration system;
- a runtime relationship discovery engine;
- a generated entity/model population framework.

The useful abstraction is the **compiled data-operation graph**.
