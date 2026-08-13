# Foundgine

Foundgine is a **semantic execution layer for .NET**.

It converts structured application intent into deterministic, authorization-preserving execution plans that can be executed by a physical provider.

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
              ┌────────┴────────┐
              ▼                 ▼
             SQL            InMemory
              │                 │
              └────────┬────────┘
                       ▼
                 Result + Evidence
```

The important boundary is simple:

> **Foundgine owns semantics, intent, authorization, planning, and execution coordination. It does not own the transport or the physical provider.**

## Product identity

Foundgine is a **semantic execution layer for .NET**. Its job is to provide one semantic boundary between structured application intent and physical execution. See [Product identity](docs/PRODUCT-IDENTITY.md) for the canonical positioning and non-goals.

## Why Foundgine exists

Modern applications expose data and operations through many different surfaces: application code, APIs, GraphQL, JSON, and increasingly AI-generated requests.

Those surfaces should not each have to rediscover the application's semantic model, authorization rules, relationships, and execution behaviour.

Foundgine introduces a common execution boundary:

```text
What the caller means
        ↓
What the application exposes
        ↓
What the caller is allowed to do
        ↓
What should be executed
        ↓
How the physical provider executes it
```

This is particularly useful when structured or AI-generated intent needs to operate against complex application data without giving the caller direct access to provider-specific operations.

## The core proposition

Foundgine's central architectural proposition is:

> **A caller can express structured intent against a semantic model; Foundgine resolves and authorizes that intent, compiles it into a deterministic execution plan, and carries the authorization semantics through to execution.**

The same core can therefore sit behind multiple intent sources and, architecturally, multiple execution providers.

## The five things Foundgine owns

### 1. Semantics

The semantic model describes what the application exposes: entities, fields, relationships, connections, and capabilities.

### 2. Intent

Intent describes what the caller requests: reading, filtering, ordering, traversal, aggregation, and mutation operations.

A GraphQL request, JSON document, or AI-generated structure is an **input representation**. It is not the semantic model itself.

### 3. Authorization

Foundgine evaluates authorization as part of the execution pipeline. Authorization is not merely a check before planning; the resulting constraints must survive into the executable plan.

### 4. Planning

Foundgine transforms authorized intent into a provider-independent execution plan.

### 5. Execution

A provider turns the execution plan into physical work and returns the result. Execution evidence can capture what was planned and executed.

## Why this is different from an ORM

An ORM primarily answers:

> How should application objects be mapped to persistence?

Foundgine answers:

> How should structured application intent become an authorized executable operation?

EF Core can therefore remain responsible for object persistence and relational configuration while Foundgine sits at a different boundary.

Foundgine deliberately does **not** attempt to provide change tracking, identity maps, lazy loading, entity materialization, or migrations.

If an application simply needs conventional object persistence, use an ORM such as EF Core. Foundgine is for the execution boundary described above.

## Why this matters for AI

AI is one important consumer of this architecture, not the definition of the core.

An AI system can generate structured intent, while Foundgine remains responsible for:

- understanding the application's semantic model;
- validating the requested operation;
- enforcing authorization;
- producing the execution plan;
- executing through a controlled provider; and
- producing execution evidence.

The model therefore does not become the authority for what it is allowed to execute.

```text
AI
 │
 │ structured intent
 ▼
Foundgine
 ├── semantics
 ├── authorization
 ├── planning
 ├── execution
 └── evidence
      │
      ▼
   provider
```

Foundgine is **not** an LLM framework, agent runtime, prompt framework, memory system, MCP implementation, or workflow engine.

## Provider independence and relationship-oriented backends

Foundgine is not tied to relational SQL. The semantic model and provider-independent execution plan deliberately separate **what the application means** from **how a backend executes it**. A relationship-oriented backend such as a Cypher/graph-database provider can therefore be added behind the same planning boundary without changing the semantic contract exposed to callers.

In practical terms, a future provider could translate the same authorized execution plan into Cypher rather than SQL. The repository's SQL and InMemory providers are the current proof points; graph/Cypher support is an extension point, not a claim that such a provider is already implemented.

## Complex application models

The API model does not have to be a 1:1 representation of persistence entities. Foundgine's semantic layer can expose models, projections, relationships, and result shapes that differ from the underlying storage entities. This is important for applications where the public/application model is richer or deliberately different from the database model.

The runtime does not need to repeatedly rediscover that mapping for every request. AOT-generated metadata and deterministic planning can move structural work out of the hot path, while provider execution remains responsible for the physical query or mutation. Complex application models should therefore be evaluated primarily on the resulting execution plan and payload, rather than assuming that every additional API shape implies an ORM-style materialization cost.

## Architectural boundaries

The dependency rules are enforced and documented in [Architecture Boundaries](docs/ARCHITECTURE-BOUNDARIES.md).

The core must not depend on the protocol used to express intent or the provider used to execute it.

```text
Intent adapters/providers

GraphQL ─┐
JSON ────┤
AI ──────┤
Code ────┘
    │
    ▼
┌───────────────────────────────┐
│           Foundgine           │
│                               │
│ Semantics                     │
│ Intent                        │
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

In particular, the semantic core must not take dependencies on GraphQL, Hot Chocolate, SQL, EF Core, OpenAI, or MCP.

## Current proof

The current repository proves the following path:

- semantic modelling and resolution;
- authorization and authorization predicates enforced at execution boundaries;
- provider-independent query and mutation planning;
- SQL execution against SQLite;
- collection-aware nested traversal;
- execution evidence and deterministic plan fingerprints;
- AOT metadata generation;
- JSON intent input; and
- a Hot Chocolate GraphQL adapter for queries and mutations.

The Banking tests provide the main end-to-end proof of the current implementation.

The repository proves a deliberately small non-SQL in-memory execution provider as an architectural proof. It does **not** claim autonomous agent execution, workflow orchestration, rollback/compensation semantics, universal provider support, or benchmark superiority.

## Projects

| Project | Purpose |
|---|---|
| `Foundgine.Abstractions` | Stable cross-layer contracts and IDs |
| `Foundgine.Metadata` | Domain and storage metadata |
| `Foundgine.Semantics` | Semantic model, intent, resolution, and authorization |
| `Foundgine.Planning` | Provider-independent execution planning |
| `Foundgine.Execution` | Execution contracts and result materialization |
| `Foundgine.InMemory` | Deliberately small CLR-backed proof provider |
| `Foundgine.Sql` | SQL execution provider |
| `Foundgine.Aot` | AOT metadata attributes/contracts |
| `Foundgine.Aot.Generator` | Roslyn metadata generator |
| `Foundgine.Intent.Json` | JSON intent adapter |
| `Foundgine.GraphQL.HotChocolate` | GraphQL query/schema adapter |
| `Foundgine.GraphQL.HotChocolate.Mutations` | GraphQL mutation adapter |

## Provider independence

Foundgine is not defined by SQL. The repository includes a deliberately small in-memory provider that consumes the same provider-independent execution plan as the SQL provider. See [Provider independence](docs/PROVIDER-INDEPENDENCE.md).

## Documentation

- [Why Foundgine](docs/WHY-FOUNDGINE.md)
- [Getting started](docs/GETTING-STARTED.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Flagship Proof](docs/FLAGSHIP-PROOF.md)
- [Execution Algebra](docs/EXECUTION-ALGEBRA.md)
- [Current status](docs/CURRENT-STATUS.md)
- [Runtime](docs/RUNTIME.md)
- [Authorization](docs/AUTHORIZATION.md)
- [AOT](docs/AOT.md)
- [Testing](docs/TESTING.md)
- [Security](docs/SECURITY.md)
- [Core contracts](docs/CORE-CONTRACTS.md)
- [GraphQL](docs/GRAPHQL.md)
- [Roadmap](docs/ROADMAP.md)
- [History](docs/history/README.md)

For AI/search context, see [`ai.seo.md`](ai.seo.md) and [`llms.txt`](llms.txt).

## Build

```powershell
dotnet test
```

The test suite is the source of truth for what is currently proven.

## Public API

See [`docs/PUBLIC-API.md`](docs/PUBLIC-API.md). Application code should prefer the stable `IFoundgine` facade (registered with `AddFoundgine`) over manually orchestrating resolution, authorization, planning, and provider execution.


**Agent boundary:** [Agent Semantic Boundary](docs/AGENT-SEMANTIC-BOUNDARY.md)
