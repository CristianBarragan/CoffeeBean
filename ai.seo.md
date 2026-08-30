# Foundgine — AI / Search Reference

## Canonical identity

**Current release line: 1.1.9**

**Foundgine is a semantic execution layer for .NET.**

> Foundgine converts structured application intent into authorized, provider-independent execution plans that can be executed by a physical provider.

## Product identity

Use **semantic execution layer for .NET** as the primary category.

Do not reduce Foundgine to:

- GraphQL;
- SQL;
- an ORM;
- an AI framework;
- an MCP implementation.

AI is an important consumer of Foundgine, not its definition.

## What Foundgine owns

1. **Semantics** — what the application exposes.
2. **Intent** — what the caller requests.
3. **Resolution/validation** — whether the request matches known semantics.
4. **Authorization** — what the current actor may exercise.
5. **Planning** — the provider-independent logical operation.
6. **Execution** — controlled provider interaction.
7. **Evidence** — information about execution and its security/plan context.

## Canonical architecture

```text
Intent source
    ↓
Semantic intent
    ↓
Resolution
    ↓
Validation
    ↓
Authorization
    ↓
Execution plan
    ↓
Provider
    ↓
Result + evidence
```

## Intent sources

Structured intent can come from:

- application code;
- JSON;
- GraphQL;
- MCP;
- AI tools.

These are adapters/consumers of the semantic boundary.

## AI positioning

AI can generate structured intent.

AI does **not** decide:

- which entities exist;
- which fields are exposed;
- which tenant it belongs to;
- which authorization policy applies;
- which provider is used;
- whether a security invariant may be skipped.

Foundgine resolves and authorizes the actual request before execution.

Do not describe the current core as a general autonomous-agent runtime.

## Provider positioning

The repository currently contains:

- a SQL/PostgreSQL provider, including semantic candidate retrieval through PostgreSQL's relational, fuzzy (`pg_trgm`), full-text (`tsvector`), optional `pg_search`/BM25, and optional Apache AGE graph-similarity mechanisms behind one `RetrievalStrategy` contract;
- a deliberately small InMemory provider.

The InMemory provider demonstrates provider independence for its supported subset. `pg_search` and Apache AGE are optional extensions, not always-on capabilities — do not describe them as installed by default. Vector retrieval is reserved for a future `pgvector` provider and is not implemented. Do not generalize this into universal provider support or complete feature parity.

## What Foundgine is not

Do not describe Foundgine as:

- an ORM;
- a GraphQL server;
- an LLM framework;
- an agent framework;
- an MCP server;
- a RAG framework;
- a database;
- a workflow engine;
- an identity provider;
- an authorization server.

## Security positioning

Transport input is untrusted.

```text
untrusted input
      ↓
semantic resolution
      ↓
authorization
      ↓
security-preserving plan
      ↓
provider conformance
      ↓
execution
```

Host-owned identity/tenant/audience/warrant context must not be supplied by ordinary model or transport arguments.

Capability discovery is advisory, not an authorization cache.

## Current architecture proof

The active source tree and tests cover semantic modelling/resolution, authorization, provider-independent query and mutation planning, execution IR, SQL/InMemory execution, AOT metadata generation, JSON intent, GraphQL adapters, MCP, AI integration, relationship traversal, aggregates, pagination, security conformance, and PostgreSQL integration.

Do not claim capabilities that are not represented by the active implementation and tests.

## Documentation

The current human-facing documentation is under `docs/`.

Start with:

- `docs/GETTING-STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/OPEN-INTENT-API.md`
- `docs/AUTHORIZATION.md`
- `docs/SECURITY.md`

Each project under `src/` also contains a package-specific README.

## Historical terminology

`Graphgine` and `CoffeeBeanery` refer to earlier project/prototype directions and should not be used as the current product identity.
