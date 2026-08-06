[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Vision**

# Vision

## Contents

- [The bold statement](#the-bold-statement)
- [Philosophy](#philosophy)
- [What Coffee Beanery is](#what-coffee-beanery-is)
- [What Coffee Beanery doesn't want to own](#what-coffee-beanery-doesnt-want-to-own)
- [Mission](#mission)
- [Vision statement](#vision-statement)
- [Roadmap by phase](#roadmap-by-phase)

---

## The bold statement

> Coffee Beanery is a compile-time execution engine that transforms business models into
> deterministic execution plans, independent of transport, database, or infrastructure.
> **Everything else is an adapter.**

If you only remember one sentence from this document, remember that one. Every other page
in this documentation set is a consequence of it.

A shorter version of the same idea, if you need an elevator pitch:

> Coffee Beanery is the compile-time execution engine for .NET applications. It transforms
> business models into deterministic execution plans while allowing developers to choose the
> best transport, persistence, and infrastructure technologies without changing the business
> model.

And the front-page version:

> Coffee Beanery separates business intent from execution. Model your domain once. Generate
> deterministic execution plans. Integrate with the best tools — not replace them.

## Philosophy

> Software should describe what the business does, not how infrastructure works.

Applications should not be written *around* SQL, GraphQL, REST, Kafka, gRPC, databases, or
ORMs. Instead, they should describe the business. Coffee Beanery transforms those business
models into optimized execution plans that different providers can execute.

**The application owns the business. Coffee Beanery owns the execution.**

```
                 Transport
        GraphQL   REST   gRPC
                │
                ▼
      Coffee Beanery Planner
                │
                ▼
         Execution Providers
      PostgreSQL  SQL Server
         Kafka     Temporal
         Redis      HTTP
                │
                ▼
          Infrastructure
```

## What Coffee Beanery is

Coffee Beanery is **not** an ORM. It is **not** a GraphQL framework. It is **not** a workflow
engine. It is **not** a database abstraction layer. It is a **compile-time execution engine**.
Its one responsibility is to transform business intent into deterministic execution plans.
Everything else is delegated to a provider.

## What Coffee Beanery doesn't want to own

This is equally important, and it's a deliberate scope boundary, not an oversight. Coffee
Beanery intentionally does not compete with the best-in-class tools it sits between:

- **Hot Chocolate** remains the GraphQL framework.
- **Dapper** remains the lightweight SQL executor.
- **EF Core** remains the mapping model that supplies metadata.
- **Kafka** remains a messaging platform (future provider — not built yet).
- **Temporal** remains a workflow engine (future provider — not built yet).

Coffee Beanery sits *between* the transport and the infrastructure, generating the execution
plan that connects them.

## Mission

**Transform business models into deterministic execution plans.** That mission does not
change as new phases are added — it's the fixed point every future provider, transport, and
adapter is judged against.

The longer form: *empower developers to model their business once and execute it everywhere
through deterministic, compile-time generated execution plans.*

## Vision statement

> To become the execution engine of modern .NET applications by separating business intent
> from infrastructure concerns through compile-time planning and provider-based execution.

## Roadmap by phase

Framing the roadmap as phases of the *same* execution engine — rather than a list of
unrelated features — is deliberate. It keeps the vision ambitious while keeping the
implementation focused, and it tells contributors that every future feature is an extension
of this idea, not a change in direction.

**Phase 1 (current)**

- EF Core mapping as the metadata source.
- Hot Chocolate as the transport.
- PostgreSQL as the execution provider.
- Dapper as the SQL executor.

**Future phases**

- Additional execution providers (SQL Server, MySQL, etc.).
- Additional transports (REST, gRPC).
- Additional infrastructure providers (Kafka, Temporal, Redis, etc.).
- Optional higher-level modeling APIs, if they solve real user problems.

See [Reference → Roadmap](../13-Reference/Roadmap.md) for the detailed, phase-by-phase
breakdown, and [Layers](Layers.md) for how today's single-solution codebase maps onto this
target architecture.

---

## Related Documentation

- [Principles](Principles.md)
- [Layers](Layers.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)
- [Reference → FAQ](../13-Reference/FAQ.md)

---

← Previous: [Architecture](README.md)  |  Next: [Principles](Principles.md) →
