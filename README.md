<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs-site/assets/logo/foundgine-logo-dark.png">
  <img src="docs-site/assets/logo/foundgine-logo.png" alt="Foundgine" width="360">
</picture>

# [Foundgine.io](https://cristianbarragan.github.io/Foundgine/docs-site/index.html)

[![NuGet Version](https://img.shields.io/nuget/v/Foundgine?label=NuGet%20Version)](https://www.nuget.org/packages/Foundgine/)
[![NuGet Downloads](https://img.shields.io/endpoint?url=https%3A%2F%2Fcristianbarragan.github.io%2FFoundgine%2Fdocs-site%2Fassets%2Ffoundgine-nuget-downloads.json&label=NuGet%20Downloads)](https://www.nuget.org/packages?q=Foundgine)
[![Unit Tests](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=unit-tests&label=Unit%20Tests)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Integration Tests](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=integration-tests&label=Integration%20Tests)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Performance](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=benchmark-build&label=Performance)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Security Audit](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=security-penetration&label=Security%20Audit)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)

# Foundgine

**Programmable semantic execution for .NET.**

Foundgine separates **what a caller wants** from **how the application executes it**.

A caller submits structured intent. Foundgine resolves that intent against an application-defined semantic model, validates it, applies authorization, builds a provider-independent execution plan, and sends that plan to a provider such as SQL or InMemory.

<p align="center"><img src="docs/assets/canonical-architecture.svg" alt="Foundgine canonical architecture: Caller → Intent → Semantic Model → Semantic Operation Graph → Retrieval → Resolution → Authorization → Plan Binding → Execution IR → Provider → Execution → Evidence. Retrieval uses parallel relational, pg_trgm fuzzy, PostgreSQL full-text, optional pg_search BM25, and optional Apache AGE graph strategies to produce candidates and evidence." width="100%"></p>

> **Canonical lifecycle:** Caller → Intent → Semantic Model → Semantic Operation Graph → Retrieval → Resolution → Authorization → Plan Binding → Execution IR → Provider → Execution → Evidence.
>
> **Retrieval is discovery, not authority:** relational lookup, `pg_trgm`, `tsvector`, optional `pg_search`/BM25, and optional Apache AGE produce candidates + evidence. Those results still pass through semantic resolution and authorization.


## Why Foundgine?

Modern applications can have many callers:

- application code;
- APIs;
- GraphQL;
- JSON clients;
- automation;
- MCP clients;
- AI agents.

Without a common semantic execution boundary, each surface tends to duplicate:

- model knowledge;
- validation;
- authorization;
- relationship traversal;
- query translation;
- provider execution.

Foundgine centralizes those concerns without making GraphQL, AI, MCP, or a database the center of the architecture.

### The tool-surface problem

This matters most where a single caller can reach many capabilities at once — an AI agent with a set of tools is the clearest case. In a typical stack, each tool is free to implement its own authorization, tenant filtering, and validation on the way to the database:

```text
Agent
 ├── Tool A → its own auth / filtering / query logic
 ├── Tool B → its own auth / filtering / query logic
 ├── Tool C → its own auth / filtering / query logic
 └── Tool D → its own auth / filtering / query logic
```

An agent with 50 tools can end up with 50 separate execution and security surfaces, each only as safe as the developer who wrote that one tool.

Foundgine gives every capability the same path instead:

```text
Agent
 ↓
Capability (structured intent)
 ↓
Foundgine — one semantic + authorization boundary
 ↓
Execution plan → provider
```

The application still defines what each capability means and who may use it. What Foundgine removes is the need for every tool, endpoint, or adapter to reimplement that decision on its own.

> **Callers describe what they want. The application defines what exists and what is allowed. Foundgine determines how the authorized meaning executes.**

## Open intent

Foundgine does not require a predefined method for every possible query.

Typed C#:

```csharp
var result = await foundgine
    .Query<Customer>()
    .Select(c => new { c.Id, c.Name })
    .Where(c => c.TenantId == tenantId)
    .Take(50)
    .ExecuteAsync();
```

Dynamic C#:

```csharp
var result = await foundgine
    .Query("Customer")
    .Select("Id", "Name")
    .Where("TenantId", SemanticFilterOperator.Eq, tenantId)
    .Take(50)
    .ExecuteAsync();
```

Both converge on the same provider-neutral semantic intent.

Open intent does **not** mean open authority. Names are resolved against the semantic model and authorization is evaluated before planning/execution.

## The architecture

The diagram above is the **canonical Foundgine architecture**. Every interface and provider-specific feature fits into this same lifecycle; individual documentation pages may zoom into one portion, but the ordering and security boundaries do not change.

### Canonical semantic lifecycle

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Semantic Operation Graph
  ↓
Retrieval
  ↓
Resolution
  ↓
Authorization
  ↓
Plan Binding
  ↓
Execution IR
  ↓
Provider
  ↓
Execution
  ↓
Evidence
```

### Retrieval is a parallel candidate-discovery stage

```text
                    Retrieval
                       │
       ┌───────────────┼───────────────┐
       ▼               ▼               ▼
   Relational       Fuzzy          FullText
   structured      pg_trgm         tsvector
       │               │               │
       ▼               ▼               ▼
     BM25          AGE Graph        Other
   pg_search       Apache AGE      strategies
       └───────────────┬───────────────┘
                       ▼
              Candidates + Evidence
                       │
                       ▼
                  Resolution
                       │
                       ▼
                  Authorization
```

Search and graph mechanisms are therefore **not alternate authorization or execution paths**. They are retrieval strategies that help resolve ambiguous references.

### The security-preserving lifecycle

The central execution artifact is the semantic operation graph. It is resolved and authorized before planning, then carried forward through immutable provenance binding into execution:

```text
Intent
  ↓
Semantic Operation Graph
  ↓
Validate + bound complexity
  ↓
Authorize against semantic contract
  ↓
Authorized Graph + Evidence
  ↓
Provider-independent Plan
  │  └─ AuthorizationBinding
  ↓
Security-preserving optimization
  ↓
ExecutionIR
  ↓
Provider Plan + Security Proof
  ↓
Final Execution Gate
  ↓
Execute
```

The key invariant is simple: **an executable provider artifact must remain traceably bound to the semantic contract and authorization decision that produced it.** Optimization may change the execution shape, but it cannot detach or weaken that security provenance.

The SQL provider isn't limited to relational access: the same semantic candidate-retrieval contract also reaches PostgreSQL's full-text/fuzzy search and, optionally, Apache AGE for graph-similarity queries — see [Providers](#providers) below.

### Semantic model

The semantic model describes application meaning:

```text
Customer
 ├── Id
 ├── Name
 └── transactions
```

It does not have to be identical to the physical persistence model.

### Metadata

Structural metadata describes facts such as entities, fields, keys, columns, and direct relationships.

```text
Metadata = what exists
Semantics = what it means / exposes
Authorization = what this actor may exercise
Intent = what the caller wants
```

### Planning

Planning produces a logical, provider-independent execution plan.

The plan does not contain SQL tables, SQL aliases, provider parameters, or GraphQL AST nodes.

### Execution

The execution layer converts the logical plan into provider execution while enforcing provider security conformance and materializing results/evidence.

## Security by boundary

Foundgine treats transport input as untrusted.

```text
untrusted intent
      ↓
resolve
      ↓
validate
      ↓
authorize
      ↓
security-preserving plan
      ↓
provider conformance
      ↓
execute
```

Identity, tenant, audience, and warrant authority come from the host/security boundary rather than model-generated or transport arguments.

Capability discovery is advisory; execution authorizes the actual request again.

For high-assurance mutation workflows, the mutation execution boundary can additionally enforce warrant validation, resource scope, security invariants, replay protection, plan binding, approval, and provider conformance.

## Providers

### SQL

`Foundgine.Sql` is the primary SQL provider. It compiles provider-independent plans to parameterized SQL and executes through ADO.NET.

PostgreSQL-specific functionality includes:

- PostgreSQL mutation compilation;
- set-based batched mutation compilation/execution;
- provider cost estimation;
- SQL security conformance.

It also implements semantic candidate retrieval — ranked, provenance-carrying matches for ambiguous references — through one provider-neutral `RetrievalStrategy` contract with several PostgreSQL-backed strategies:

| Strategy | PostgreSQL mechanism |
|---|---|
| `Fuzzy` | `pg_trgm` |
| `FullText` | `tsvector` / `websearch_to_tsquery` |
| `Search` | optional `pg_search` / BM25 |
| `GraphSimilarity` | optional Apache AGE (Cypher over a semantic relationship) |
| `Vector` | reserved for a future `pgvector` provider |

`Fuzzy` and `FullText` need nothing beyond PostgreSQL itself. `Search` and `GraphSimilarity` are optional and only activate when `pg_search` or Apache AGE are installed. Whichever strategy runs, retrieval only produces candidates and evidence — the result still goes through ordinary semantic resolution and authorization before it can appear in an execution plan.

### InMemory

`Foundgine.InMemory` executes a deliberately limited subset of the same logical plan over CLR-backed rows.

It exists primarily to prove provider independence and support fast deterministic tests.

## Interfaces and adapters

Foundgine can sit below several interfaces:

```text
Foundgine.GraphQL.HotChocolate
Foundgine.Intent.Json
Foundgine.MCP
Foundgine.AI
```

These adapters translate their input into the Foundgine semantic boundary. They do not create alternate execution architectures.

## AOT

`Foundgine.Aot` and `Foundgine.Aot.Generator` move stable metadata discovery into compilation:

```text
AOT declarations
      ↓
Roslyn generator
      ↓
generated metadata
      ↓
semantic model
      ↓
runtime
```

This is designed for Native-AOT-friendly metadata discovery. It does not make arbitrary application/provider dependencies automatically AOT-compatible.

## Mutations

Mutations use a separate semantic/planning/execution path because writes require explicit dependencies and stronger security guarantees.

```text
Semantic mutation graph
        ↓
Mutation plan
        ↓
Dependency levels
        ↓
Security/conformance
        ↓
Provider execution
```

All mutation transports should converge on this boundary.

Writes are where this matters most, because the cost of a wrong authorization decision is much higher than for a read. `samples/Foundgine.HighAssurance.Banking` is the concrete proof case: a `TransferFunds` mutation whose execution boundary revalidates tenant, ownership, account state, and daily limits, holds deterministic locks across both accounts, and produces an audit entry and execution receipt — while making no claim that Foundgine infers financial policy from natural language. See its [README](samples/Foundgine.HighAssurance.Banking/README.md) for the full walkthrough.

## Package map

| Package | Role |
|---|---|
| `Foundgine` | Application-facing runtime facade |
| `Foundgine.Abstractions` | Shared contracts and stable identifiers |
| `Foundgine.Semantics` | Semantic model, intent, resolution, authorization |
| `Foundgine.Metadata` | Structural metadata and discovery |
| `Foundgine.Planning` | Provider-independent planning and rewrites |
| `Foundgine.Execution` | Execution IR, provider boundary, evidence/security |
| `Foundgine.Sql` | SQL/PostgreSQL provider |
| `Foundgine.InMemory` | In-memory proof/test provider |
| `Foundgine.Aot` | AOT declarations and generated helpers |
| `Foundgine.Aot.Generator` | Roslyn source generator |
| `Foundgine.Intent.Json` | JSON intent adapter |
| `Foundgine.MCP` | Model Context Protocol adapter |
| `Foundgine.AI` | Microsoft.Extensions.AI integration |
| `Foundgine.GraphQL.HotChocolate` | GraphQL query adapter |
| `Foundgine.GraphQL.HotChocolate.Execution` | Secure GraphQL query execution |
| `Foundgine.GraphQL.HotChocolate.Mutations` | GraphQL mutation adapter |
| `Foundgine.GraphQL.HotChocolate.MutationExecution` | Secure GraphQL mutation execution |
| `Foundgine.Security.Authority` | Optional authority/recovery control-plane infrastructure |

All packages target .NET 9 except the Roslyn generator, which targets `netstandard2.0`.

### Minimum footprint

17 packages looks like a lot, but a basic application only ever installs two of them explicitly:

- `Foundgine` — the facade;
- one provider — `Foundgine.Sql` or `Foundgine.InMemory`.

`Foundgine` transitively brings in `Foundgine.Semantics`, `Foundgine.Metadata`, `Foundgine.Planning`, `Foundgine.Execution`, and `Foundgine.Abstractions` — those are implementation layers, not separate things you choose between. The remaining packages are each optional and additive by design, not modularity for its own sake:

- `Foundgine.Aot` / `Foundgine.Aot.Generator` — only needed for attribute-driven, source-generated metadata instead of runtime discovery.
- `Foundgine.Intent.Json`, `Foundgine.MCP`, `Foundgine.AI`, `Foundgine.GraphQL.HotChocolate*` — one package per caller-facing interface, so a project that only exposes GraphQL doesn't pull in Hot Chocolate's, MCP's, or Microsoft.Extensions.AI's dependency trees for interfaces it never uses.
- `Foundgine.Security.Authority` — a substantial, independently-versioned recovery/control-plane subsystem (warrants, quorum, witnesses) that the large majority of applications will never need.

If you're only trying it out: `Foundgine` + `Foundgine.InMemory` is the entire footprint.

## Samples

The repository contains progressively more advanced examples, including the SupplyChain semantic sample.

Start with:

- `samples/Foundgine.SupplyChain.Simple`
- `samples/Foundgine.SupplyChain`
- `samples/Foundgine.SupplyChain.Semantic`
- `samples/Foundgine.HighAssurance.Postgres`
- `samples/Foundgine.Agent.OpenAI`

The SupplyChain samples are also useful as architecture tests: they show how API, application, domain, metadata/AOT, semantics, authorization, planning, and PostgreSQL execution fit together.

## Documentation

The docs are meant to be read in order, each page linking to the next: start at [`docs/GETTING-STARTED.md`](docs/GETTING-STARTED.md) and follow the "Next" link at the bottom of each page, or use the full list in [`docs/README.md`](docs/README.md).

Every project under `src/` has its own package-level README describing its responsibility, API boundary, security considerations, and relationship to the rest of Foundgine.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

PostgreSQL integration testing is documented in [`docs/POSTGRES-E2E.md`](docs/POSTGRES-E2E.md).

## Current release line

The repository is on **1.1.7** and targets **.NET 9**.

Foundgine is licensed under the MIT license.
