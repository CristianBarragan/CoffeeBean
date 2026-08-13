# Product identity

## Canonical identity

**Foundgine is a semantic execution layer for .NET.**

> Foundgine converts structured application intent into deterministic, authorization-preserving execution plans that can be executed by a physical provider.

That sentence is the canonical description of the product. Other descriptions should explain it, not replace it.

## The problem Foundgine solves

Applications increasingly have multiple ways to request work against the same domain:

- application code;
- HTTP/JSON APIs;
- GraphQL;
- structured automation;
- AI-generated intent.

Foundgine provides one semantic boundary between those requests and physical execution.

```text
Intent source
     ↓
Structured intent
     ↓
Semantic model + resolution
     ↓
Authorization
     ↓
Provider-independent execution plan
     ↓
Physical provider
     ↓
Result + evidence
```

## What Foundgine owns

Foundgine owns the semantic execution boundary:

1. **Semantics** — what the application exposes.
2. **Intent** — what the caller requests.
3. **Resolution** — how that intent maps onto known semantics.
4. **Authorization** — what the resolved request is allowed to do.
5. **Planning** — the provider-independent executable representation.
6. **Execution coordination** — passing the authorized plan and runtime context to a provider.
7. **Evidence** — information about the execution that the runtime chooses to record.

## What Foundgine does not own

Foundgine does not define:

- the transport protocol;
- the GraphQL server;
- the database engine;
- an ORM object-relational persistence model;
- an LLM or agent runtime;
- workflow orchestration;
- identity management.

Those concerns can sit around Foundgine or provide implementations behind its contracts.

## The important boundaries

### Intent is not authority

An external caller, including an AI system, can produce intent. It cannot define the application's semantics or grant itself authorization.

### Planning is not SQL

The execution plan describes logical work. A provider translates that plan into physical execution.

### Authorization is not a transport check

Authorization is part of semantic execution. Constraints discovered during authorization must remain represented in the executable plan where required by the provider.

### AI is a consumer, not the product definition

AI is an important reason this boundary is useful, but Foundgine is not an AI framework. The core remains useful for ordinary application code, JSON, GraphQL, and other structured intent sources.

## What the current repository proves

The active repository currently demonstrates:

- semantic modelling and resolution;
- granular authorization;
- provider-independent query and mutation planning;
- SQL/SQLite execution;
- a deliberately small in-memory provider consuming the same logical plan for its tested subset;
- JSON intent;
- Hot Chocolate GraphQL adapters;
- AOT metadata generation; and
- execution evidence.

These proofs do not establish universal provider support, autonomous-agent execution, workflow orchestration, or benchmark superiority.

## Historical material

`archive/` and `docs/history/` contain earlier designs and prototypes, including the previous Graphgine direction. They are historical reference material and are not part of the current product identity.
