# M9 — Foundgine Value Proposition Review

## Purpose

This milestone is a decision checkpoint, not a feature port.

The question is not whether Foundgine can execute queries. The current source already demonstrates a semantic request → resolution → authorization → planning → provider path, plus AOT and a GraphQL adapter.

The question is whether that architecture earns its complexity compared with a conventional application built around EF Core + Hot Chocolate.

## Evidence from the current source

The current source contains separate projects for:

- Abstractions
- Metadata
- Semantics
- Planning
- Execution
- AOT
- SQL
- GraphQL
- GraphQL mutations

The core source contains roughly 80 C# files. The largest implementation areas are the GraphQL adapter, SQL compiler, mutation planner, semantic resolver, SQL writer, and mutation compiler.

This is meaningful complexity. It must therefore provide a capability that conventional application stacks do not already provide more simply.

## What Foundgine is not

Foundgine should not compete with EF Core on:

- object tracking;
- change tracking;
- LINQ-to-objects/domain persistence convenience;
- migrations;
- relationship fix-up;
- general CRUD persistence.

It should not compete with Hot Chocolate on GraphQL protocol execution.

It should not compete with Dapper on being a thin SQL mapper.

The existing documentation already makes these boundaries explicit.

## The actual differentiator

The strongest architectural value is a **provider-independent semantic execution layer** between an external intent producer and physical persistence.

```text
External intent
      ↓
SemanticRequest
      ↓
Resolution
      ↓
Authorization
      ↓
SemanticGraph
      ↓
ExecutionPlan
      ↓
Provider
```

That creates four capabilities that are difficult to obtain cleanly by simply composing EF Core and Hot Chocolate:

### 1. Dynamic semantic intent

The request can describe entities, fields, relationships, filters, ordering and traversal without embedding SQL or ORM expression trees.

This gives GraphQL, an API adapter, a future agent, or another structured-intent producer the same engine entry point.

### 2. Authorization before physical planning

Authorization operates on the resolved semantic graph before provider-specific execution is produced.

That gives policy enforcement a stable representation independent of SQL shape.

### 3. Provider-independent planning

The planner produces an execution plan rather than SQL.

This is valuable only if more than one provider or execution strategy is actually needed. With one SQL provider forever, the abstraction has a much weaker justification.

### 4. AOT/static domain knowledge

The AOT path can produce metadata without requiring runtime discovery of the domain model.

This matters for Native AOT, startup determinism, trimming, and environments where reflection-heavy discovery is undesirable.

## Where Foundgine is currently overkill

For a conventional application with:

```text
GraphQL
  ↓
EF Core
  ↓
SQL database
```

Foundgine currently adds substantial machinery without automatically delivering a better developer experience.

A simple CRUD application should prefer EF Core + Hot Chocolate.

Likewise, a service with one fixed set of queries and one SQL provider has little reason to introduce a semantic planner.

## The Foundgine target use case

Foundgine becomes justified when several of these are true at the same time:

- query shape is dynamic;
- relationships are selected dynamically;
- authorization depends on semantic relationships or paths;
- multiple intent producers should share one execution engine;
- multiple providers/execution strategies are plausible;
- compile-time metadata is valuable;
- an AI/LLM may produce structured requests;
- execution must remain deterministic after intent is produced;
- the system needs to explain or inspect the semantic plan before execution.

The more of these requirements disappear, the less useful Foundgine becomes.

## AI / LLM relevance

The strongest future case is not putting an LLM inside Foundgine.

The useful boundary is:

```text
Natural language
      ↓
LLM / parser
      ↓
Structured SemanticRequest
      ↓
Foundgine
      ↓
Resolve + Authorize + Plan
      ↓
Deterministic execution
```

The LLM proposes intent. Foundgine validates what exists, enforces policy, and determines what may actually execute.

That gives the LLM a constrained action surface rather than database access.

## Decision

**Foundgine earns its architectural complexity only as a semantic execution substrate.**

It does not earn the complexity by being another ORM or GraphQL server.

Therefore:

1. Keep M1–M7 architectural boundaries.
2. Keep the semantic request/graph model.
3. Keep authorization before provider planning.
4. Keep provider-independent planning only while provider independence is a real requirement.
5. Keep AOT as an optional compile-time path, not as a mandatory runtime abstraction.
6. Keep GraphQL outside the core.
7. Treat mutations and advanced query features as post-M7 capabilities that must justify each abstraction individually.
8. Do not add GraphQL syntax features merely to increase milestone count.

## Complexity budget

Every new abstraction should answer one question:

> What capability does this provide that a simpler composition of EF Core, Hot Chocolate, Dapper/ADO.NET, or application code cannot provide cleanly?

If the answer is only "it keeps the architecture consistent", that is not enough.

If the answer is semantic authorization, provider-independent planning, deterministic AI intent execution, or AOT/static topology, the abstraction may be justified.

## Next architectural test

The next work should not be another feature milestone.

Build one representative **AI/dynamic-intent acceptance path** using the existing semantic contracts, without adding an LLM dependency:

```text
Structured intent
      ↓
SemanticRequest
      ↓
Resolve
      ↓
Authorize
      ↓
Plan
      ↓
SQLite
```

Then compare the amount of code and indirection required with an equivalent EF Core + application-policy implementation.

That comparison is the real test of whether Foundgine deserves to exist.
