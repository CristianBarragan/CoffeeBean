<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs-site/assets/logo/foundgine-logo-dark.png">
  <img src="docs-site/assets/logo/foundgine-logo.png" alt="Foundgine" width="360">
</picture>

# [Foundgine.io](https://cristianbarragan.github.io/Foundgine/docs-site/index.html)

[![NuGet Version](https://img.shields.io/nuget/v/Foundgine?label=NuGet%20Version)](https://www.nuget.org/packages/Foundgine/)
[![NuGet Downloads](https://img.shields.io/endpoint?url=https%3A%2F%2Fcristianbarragan.github.io%2FFoundgine%2Fdocs-site%2Fassets%2Ffoundgine-nuget-downloads.json)](https://www.nuget.org/packages?q=Foundgine)
[![Unit Tests](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=unit-tests&label=Unit%20Tests)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Integration Tests](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=integration-tests&label=Integration%20Tests)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Performance](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=benchmark-build&label=Performance)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)
[![Security Audit](https://img.shields.io/github/actions/workflow/status/CristianBarragan/Foundgine/build.yml?branch=main&job=security-penetration&label=Security%20Audit)](https://github.com/CristianBarragan/Foundgine/actions/workflows/build.yml)

# Foundgine

**Programmable semantic execution for .NET.**

Foundgine separates **what a caller wants** from **how the application executes it**.

A caller submits structured intent. Foundgine resolves that intent against an application-defined semantic model, validates it, applies authorization, builds a provider-independent execution plan, and sends that plan to a provider such as SQL or InMemory.

```text
Caller / transport
        │
        ▼
      Intent
        │
        ▼
  Semantic Model
        │
        ▼
 Resolution + Validation
        │
        ▼
   Authorization
        │
        ▼
 Provider-independent Plan
        │
        ▼
     Execution
        │
        ▼
 SQL / InMemory / future providers
        │
        ▼
      Result
```

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

```text
                    Intent sources
        ┌─────────────┼─────────────┐
        │             │             │
       C#          GraphQL          MCP
        │             │             │
       JSON           AI / Agents   │
        └─────────────┼─────────────┘
                      ▼
              ┌───────────────┐
              │   Foundgine   │
              │               │
              │   Semantics   │
              │ Authorization │
              │   Planning    │
              │   Execution   │
              └───────┬───────┘
                      │
               ┌──────┴──────┐
               ▼             ▼
              SQL         InMemory
```

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

- `pg_trgm` fuzzy retrieval;
- PostgreSQL full-text search;
- optional `pg_search`/BM25 retrieval;
- PostgreSQL mutation compilation;
- set-based batched mutation compilation/execution;
- provider cost estimation;
- SQL security conformance.

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

Start with [`docs/GETTING-STARTED.md`](docs/GETTING-STARTED.md), then read:

1. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
2. [`docs/OPEN-INTENT-API.md`](docs/OPEN-INTENT-API.md)
3. [`docs/AUTHORIZATION.md`](docs/AUTHORIZATION.md)
4. [`docs/SECURITY.md`](docs/SECURITY.md)

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
