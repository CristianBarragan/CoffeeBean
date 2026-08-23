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


## NuGet package ecosystem

Foundgine is distributed as a coordinated set of NuGet packages rather than a single monolithic library. This gives users a clear path from provider-independent contracts and semantics through planning and execution to SQL, AI, MCP, GraphQL, AOT, and high-assurance authorization components.

### MCP discovery

Foundgine includes a first-class MCP adapter in `src/Foundgine.MCP`. The repository-level [`mcp.json`](mcp.json) describes the MCP integration and its exposed tools for automated ecosystem discovery. Hosts expose the MCP Streamable HTTP transport at `/mcp`; identity, tenant context, and authorization remain host-owned and are supplied through `SecurityExecutionContext`.

The same server metadata is available at [`.well-known/mcp.json`](.well-known/mcp.json) for discovery-oriented tooling.

## Independent security assessments

<a href="https://www.unofficialos.com/tool/foundgine" target="_blank"><img width="181" height="50" alt="image" src="https://github.com/user-attachments/assets/50c36f89-ae92-489b-b502-45da52f787a3" />

<a href="https://similarlabs.com/p/foundgine-programmable-semantic-execution-platform" target="_blank"><img width="181" height="50" alt="image" src="https://similarlabs.com/_next/static/media/logo.b5015d3b.svg" />

<a href="https://aitop10.tools/" target="_blank"><img width="280" height="62" alt="image" src="https://github.com/user-attachments/assets/2b01013d-90a9-493c-af47-afdf4f3b5a40" />

<a href="https://dofollow.tools" target="_blank"><img src="https://dofollow.tools/badge/badge_light.svg" alt="Featured on Dofollow.Tools" width="200" height="54" /></a>

**NuGet snapshot:** latest version **0.5.2**, targeting **.NET 9.0**, with **18 published packages**. The **NuGet Downloads** badge above represents this package-ecosystem total, not just the `Foundgine` core package.

| Package | Downloads | Role |
|---|---:|---|
| [Foundgine](https://www.nuget.org/packages/Foundgine) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-562-blue)](https://www.nuget.org/packages/Foundgine) | Semantic execution layer for .NET. Resolves structured intent into authorized, deterministic execution plans. |
| [Foundgine.Abstractions](https://www.nuget.org/packages/Foundgine.Abstractions) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-1%2C137-blue)](https://www.nuget.org/packages/Foundgine.Abstractions) | Provider-independent contracts and identifiers used by Foundgine. |
| [Foundgine.Semantics](https://www.nuget.org/packages/Foundgine.Semantics) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-1%2C010-blue)](https://www.nuget.org/packages/Foundgine.Semantics) | Semantic intent, resolution, authorization and request model. |
| [Foundgine.Planning](https://www.nuget.org/packages/Foundgine.Planning) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-811-blue)](https://www.nuget.org/packages/Foundgine.Planning) | Provider-independent execution planning. |
| [Foundgine.Metadata](https://www.nuget.org/packages/Foundgine.Metadata) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-688-blue)](https://www.nuget.org/packages/Foundgine.Metadata) | Semantic metadata model and runtime metadata registry. |
| [Foundgine.Execution](https://www.nuget.org/packages/Foundgine.Execution) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-765-blue)](https://www.nuget.org/packages/Foundgine.Execution) | Execution contracts, provider boundary and execution coordination. |
| [Foundgine.Sql](https://www.nuget.org/packages/Foundgine.Sql) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-485-blue)](https://www.nuget.org/packages/Foundgine.Sql) | SQL execution provider and PostgreSQL mutation/query compilation. |
| [Foundgine.Aot](https://www.nuget.org/packages/Foundgine.Aot) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-474-blue)](https://www.nuget.org/packages/Foundgine.Aot) | AOT metadata attributes and runtime support for generated metadata. |
| [Foundgine.InMemory](https://www.nuget.org/packages/Foundgine.InMemory) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-497-blue)](https://www.nuget.org/packages/Foundgine.InMemory) | In-memory execution provider for testing and development. |
| [Foundgine.Intent.Json](https://www.nuget.org/packages/Foundgine.Intent.Json) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-571-blue)](https://www.nuget.org/packages/Foundgine.Intent.Json) | JSON intent adapter for semantic requests. |
| [Foundgine.AI](https://www.nuget.org/packages/Foundgine.AI) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-315-blue)](https://www.nuget.org/packages/Foundgine.AI) | AI tool integration using `Microsoft.Extensions.AI`. |
| [Foundgine.MCP](https://www.nuget.org/packages/Foundgine.MCP) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-282-blue)](https://www.nuget.org/packages/Foundgine.MCP) | MCP adapter for exposing semantic capabilities and provider-neutral intent. |
| [Foundgine.GraphQL.HotChocolate](https://www.nuget.org/packages/Foundgine.GraphQL.HotChocolate) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-522-blue)](https://www.nuget.org/packages/Foundgine.GraphQL.HotChocolate) | Hot Chocolate adapter that converts GraphQL selections into Foundgine semantic requests. |
| [Foundgine.Agent.OpenAI](https://www.nuget.org/packages/Foundgine.Agent.OpenAI) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-318-blue)](https://www.nuget.org/packages/Foundgine.Agent.OpenAI) | OpenAI agent integration for Foundgine. |
| [Foundgine.GraphQL.HotChocolate.Mutations](https://www.nuget.org/packages/Foundgine.GraphQL.HotChocolate.Mutations) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-450-blue)](https://www.nuget.org/packages/Foundgine.GraphQL.HotChocolate.Mutations) | Hot Chocolate mutation adapter for Foundgine. |
| [Foundgine.CoffeeBeanery.ProductComposite](https://www.nuget.org/packages/Foundgine.CoffeeBeanery.ProductComposite) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-186-blue)](https://www.nuget.org/packages/Foundgine.CoffeeBeanery.ProductComposite) | Product-composite integration package. |
| [Foundgine.Authorization](https://www.nuget.org/packages/Foundgine.Authorization) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-30-blue)](https://www.nuget.org/packages/Foundgine.Authorization) | Provider-agnostic authorization recovery control plane with witness quorum, credential lifecycle, journal reconciliation and failover. |
| [Foundgine.HighAssurance.Postgres](https://www.nuget.org/packages/Foundgine.HighAssurance.Postgres) | [![NuGet Downloads](https://img.shields.io/badge/NuGet%20Downloads-29-blue)](https://www.nuget.org/packages/Foundgine.HighAssurance.Postgres) | PostgreSQL high-assurance authorization and execution support. |


### Why the package ecosystem matters

The package set makes the architecture visible and independently consumable:

```text
                    Foundgine
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
   Abstractions     Semantics      Metadata
        │              │              │
        └──────────────┼──────────────┘
                       ▼
                    Planning
                       │
                       ▼
                   Execution
                  /    |     \
                 /     |      \
               SQL   InMemory   AOT
                │
                ▼
            PostgreSQL

        Intent / Interface adapters
        ├── JSON
        ├── MCP
        ├── AI
        ├── OpenAI Agent
        └── GraphQL / Hot Chocolate

        High-assurance controls
        ├── Authorization
        └── HighAssurance.Postgres
```

The download numbers are **NuGet-reported package downloads, not unique users or installations**. They are included as an adoption signal and should be interpreted together with the automated test/security gates, published benchmarks, documentation, and end-to-end examples.

## From intent to authorized execution.

**Foundgine is a programmable semantic execution platform for .NET.**

It creates a controlled boundary between application callers — including APIs, GraphQL, automation, and AI agents — and the data and operations they are allowed to execute.

Instead of allowing every caller to implement its own validation, authorization, query translation, and data-access logic, Foundgine turns structured intent into an authorized execution plan and executes that plan through a provider.

```text
Caller
  │
  ▼
Intent
  │
  ▼
Semantic Model
  │
  ▼
Authorization
  │
  ▼
Execution Plan
  │
  ▼
Provider
  │
  ▼
Result
```

## What is Foundgine?

Foundgine separates **what a caller wants** from **how the application executes it**.

A caller submits structured intent. Foundgine resolves that intent against an application-defined semantic model, validates the requested capabilities, applies authorization constraints, builds an execution plan, and sends the plan to a provider such as SQL or InMemory.

The result is a reusable execution boundary that can sit underneath multiple interfaces.

```text
                 Intent Sources

     API       GraphQL       Automation       AI Agent
       \          |              |              /
        \         |              |             /
         └────────┴──────────────┴─────────────┘
                          │
                          ▼
                  ┌───────────────┐
                  │   Foundgine   │
                  │               │
                  │ Semantic      │
                  │ Authorization │
                  │ Planning      │
                  │ Execution     │
                  └───────┬───────┘
                          │
              ┌───────────┼───────────┐
              ▼           ▼           ▼
             SQL       InMemory     Providers
```

## Why does Foundgine exist?

Modern applications increasingly have many callers:

- web and mobile applications
- APIs
- GraphQL clients
- internal services
- automation
- AI agents

Without a common execution boundary, each interface can grow its own authorization, validation, query translation, and data-access path.

Foundgine is designed to centralize the semantic execution model so that different callers can share the same application-defined capabilities and execution rules.

### The key idea

> **Callers describe what they want. Foundgine determines what is allowed, how it should execute, and which provider performs it.**

## Security conformance

Foundgine treats security requirements as part of the semantic execution contract. Required security invariants are propagated into plans and checked against provider capabilities before execution. This prevents a provider from silently executing a capability whose security guarantees it cannot preserve.

The security progression currently includes security invariant registration, plan-level invariant proof, SQL provider conformance, high-assurance mutation conformance, and cross-provider conformance.


# Foundgine and AI agents

AI agents make this boundary particularly important.

An AI model can decide what it wants to accomplish. It should not become the authority over which application data it is allowed to access, nor should it need direct database credentials.

Instead:

```text
AI Agent
    │
    │ structured intent
    ▼
Foundgine
    ├── resolve
    ├── validate
    ├── authorize
    ├── plan
    └── execute
            │
            ▼
        PostgreSQL
```

This is deliberately different from:

```text
AI → generate SQL → database
```

Foundgine is intended to keep the application in control of authorization and execution while allowing AI and other structured callers to use application capabilities.

## Website

# [Foundgine.io](https://cristianbarragan.github.io/Foundgine/docs-site/index.html)

## Capabilities

| Capability | Purpose |
|---|---|
| Semantic modeling | Define the application-facing model independently of physical persistence details |
| Structured intent | Represent requested operations without coupling callers directly to SQL |
| Relationship traversal | Express operations across connected domain data |
| Authorization-aware planning | Carry application authorization constraints into execution planning |
| Execution planning | Convert semantic operations into provider-independent plans |
| Plan rewriting and optimization | Transform plans before physical execution |
| Provider independence | Separate semantic operations from provider-specific execution |
| SQL execution | Execute relational plans against SQL providers |
| InMemory execution | Execute the same semantic model without a database |
| GraphQL integration | Use GraphQL as an interface without making GraphQL the execution model |
| JSON / structured input | Accept structured intent from non-GraphQL callers |
| AOT support | Support generated metadata and Native AOT-oriented deployments |
| AI-agent integration | Allow agents to request application capabilities without direct database authority |
| Execution evidence | Make authorization, planning, and execution observable |

## A 30-second example

A caller asks:

```text
Find customers with accounts over $10,000.
```

The caller does not need to know the database schema or generate SQL.

Conceptually:

```text
Request
  ↓
Customer
  └── Accounts
        └── Balance > 10,000
  ↓
Authorization
  ↓
Execution plan
  ↓
SQL provider
  ↓
Result
```

The important boundary is:

```text
What the caller requested
          ≠
What the database can execute
```

Foundgine connects those two through an application-controlled semantic and planning layer.

## Performance evidence

The 12 August 2026 CoffeeBeanery benchmark contains three successful runs over a deterministic PostgreSQL graph workload.

At concurrency 32:

| Implementation | Average RPS | Average p95 |
|---|---:|---:|
| Hot Chocolate + EF Core | 139.4 | 338.4 ms |
| Foundgine — no cache | 2,781.0 | 20.3 ms |
| Foundgine — provider-plan cache | 2,838.9 | 19.9 ms |

That corresponds to approximately **20.0× the throughput** of the baseline without the cache and **20.4× with the cache** for this workload.

The benchmark also reports zero application errors, zero request timeouts, and zero cancelled requests across the three successful runs.

These results are workload-specific evidence, not a universal claim that Foundgine is faster than every EF Core or GraphQL workload.

See [`docs-site/performance/index.md`](docs-site/performance/index.md) for the full query benchmark methodology and caveats.

### Agent benchmark

A separate suite measures the AI-agent path specifically: how an agent calling Foundgine through MCP compares to an agent calling a conventional EF Core path directly, across workload size and concurrency. It covers tool-call count, throughput, wall time, and estimated per-transaction token/context load.

See [`docs-site/agent-benchmark/index.html`](docs-site/agent-benchmark/index.html) for the interactive workload/concurrency matrix and the per-run write-ups.

## The verification story

Foundgine is designed to be evaluated as an execution system, not as a single benchmark number. The repository's verification path moves from deterministic behavior to real database execution, then to hostile inputs and performance, before the Supply Chain benchmark exercises the complete agent-facing boundary.

```text
                 Foundgine verification
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
   Unit tests       Integration       Security
        │           PostgreSQL       penetration
        │                │          + adversarial input
        └────────────────┼────────────────┘
                         ▼
                Performance smoke
                         │
                         ▼
             Supply Chain E2E story
                         │
          Agent → MCP → Foundgine → PostgreSQL
```

### The gates

| Gate | Purpose | What passing means |
|---|---|---|
| **Unit tests** | Verify deterministic semantic, planning, authorization and runtime behavior | The core contracts remain correct without external infrastructure |
| **Integration tests** | Exercise the real PostgreSQL provider | Provider execution preserves the tested semantics against a real database |
| **Authorization penetration** | Attack high-assurance authorization paths | Unauthorized operations are rejected through the real execution path |
| **Adversarial semantic-input tests** | Replay hostile model input and adversarial engine cases | Malicious or malformed intent remains inside the semantic security boundary |
| **Performance smoke** | Run real benchmark traffic in Docker | The benchmark stack can seed, start, execute and finish without errors |
| **Supply Chain E2E** | Exercise the complete agent-facing business workflow | Stateful capabilities, authorization, mutation integrity and replay protection work together |

The CI release workflow makes the unit, PostgreSQL integration, authorization penetration, adversarial security and performance jobs prerequisites for package publication. The Supply Chain E2E is an additional product-level benchmark and is intentionally reported separately.

### The complete story

A caller starts with **intent**, not SQL. Foundgine resolves that intent against an application semantic model, applies authorization and validation, creates an execution plan, lowers the plan through ExecutionIR and sends the executable representation to a provider.

The Supply Chain benchmark makes that architecture concrete:

```text
AI-agent-like workload
        │
        ▼
       MCP
        │  capability boundary
        ▼
    Foundgine
        ├── semantic resolution
        ├── authorization
        ├── validation
        ├── planning
        ├── ExecutionIR
        └── execution evidence
        │
        ▼
     PostgreSQL
        │
        ▼
  state + transaction result
```

The workload includes customers, orders, order items, products, suppliers, categories, inventory, warehouses, shipments and carriers. It mixes valid, invalid and unauthorized operations across customer, customer-service, warehouse, procurement and administrator identities. `PlaceOrder` is the high-assurance vertical slice: authorization, ownership, validation, server-side pricing, inventory checks, atomic mutation, idempotency/replay protection and execution evidence.

This is the distinction Foundgine is trying to establish: **the agent can request a capability, but the agent does not become the authority that defines how the capability is authorized or executed.**

For the complete Supply Chain scope and reproducible report, see [`benchmarks/AgentEndToEnd/SupplyChain/README.md`](benchmarks/AgentEndToEnd/SupplyChain/README.md) and the [interactive Supply Chain E2E report](docs-site/agent-benchmark/supply-chain/index.html).

## What Foundgine is not

Foundgine is not:

- an ORM replacement
- a database
- a GraphQL server
- an LLM
- an agent framework
- an identity provider

It is an execution layer that can sit underneath those kinds of systems.

## Vision

> **Make application capabilities understandable and safely executable by machines.**

The long-term vision is a stable semantic execution boundary between **what a system asks for** and **what an application is willing to execute**.

That boundary should work for traditional software and intelligent agents alike.

## Documentation

- [What is Foundgine?](docs-site/what-is-foundgine.md)
- [AI agents and PostgreSQL](docs-site/ai-agents/index.md)
- [Architecture](docs-site/architecture/index.md)
- [Performance](docs-site/performance/index.md)
- [llms.txt](docs-site/llms.txt) / [llms-full.md](docs-site/llms-full.md) — machine-readable documentation index for AI agents and LLM tooling

The published site (built from `docs-site/`) is available at https://cristianbarragan.github.io/Foundgine/.

## Development

Repository development setup, tests, benchmarks, deployment templates, and contribution guidance should remain separate from the first-time user experience.

Source data for the published benchmarks lives under [`benchmarks/`](benchmarks/): the query benchmark in [`benchmarks/CoffeeBeanery.Performance/`](benchmarks/CoffeeBeanery.Performance/), and the agent-path benchmark in [`benchmarks/AgentEndToEnd/`](benchmarks/AgentEndToEnd/), each with the runner and raw per-run artifacts that the corresponding `docs-site/` page is built from.

## Security

Foundgine's authorization and execution boundaries are intended to reduce unsafe access paths, but application security remains a shared responsibility. Authentication, secret management, transport security, rate limiting, database permissions, and deployment security remain application and infrastructure responsibilities.

## Status

Foundgine is actively evolving (current version: 0.5.0). Public API stability, provider coverage, AI-agent integrations, and production deployment patterns should be treated according to the project's current release and compatibility policy.

Detailed, dated engineering notes for each release are kept in [`CHANGELOG.md`](CHANGELOG.md).


## High-assurance mutation security details

Mutation cancellation is propagated to the provider execution boundary and cannot commit after a cancellation check fails.

### Authorization context lifecycle security

The PostgreSQL high-assurance authorization context is now lifecycle-safe. Actor/tenant identity is immutable, versions are strictly monotonic, deleted identities retain a version tombstone, and missing configured authorization context fails closed. Lifecycle writes use the same row-lock serialization boundary as mutation authorization reads.

### Authorization context cryptographic integrity

Persisted PostgreSQL authorization evidence is cryptographically bound to its complete canonical security payload with an externally held HMAC-SHA256 key, backed by an authorized external key lifecycle with active/verification-only/retired states, monotonic rotation provenance, atomic immutable ring snapshots, and safe retirement checks against persisted evidence. Unknown keys, algorithm mismatches, altered actor/tenant/state/version/fingerprint values, and tampered lifecycle tombstones fail closed. Key rotation is supported through a verification key ring while cryptographic material remains outside the database and cache identity.
