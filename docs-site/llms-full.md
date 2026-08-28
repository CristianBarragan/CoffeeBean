# Foundgine 1.1.0

Current shipped release: **1.1.0** — an additive 1.x release with a secure GraphQL query executor, shared host-owned security execution-context provider, and a source-integrated Supply Chain getting-started sample.

# Foundgine — Full Documentation

> This file concatenates all public Foundgine documentation pages for full-context ingestion by AI agents and LLMs. See llms.txt for a linked index of the same pages.

---

## What Is Foundgine?


**Foundgine is a semantic execution platform for .NET that turns structured intent into authorized execution plans.**

## The problem

Applications increasingly have multiple callers:

- APIs
- GraphQL
- automation
- internal services
- AI agents

Each caller can otherwise grow its own rules for validation, authorization, query construction, and data access.

That makes the application harder to reason about and creates multiple execution paths.

## The Foundgine model

Foundgine introduces a semantic boundary:

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Authorization
  ↓
Execution Plan
  ↓
Provider
  ↓
Result
```

The caller expresses an operation. The application defines the semantic capabilities and authorization rules. Foundgine builds the execution plan and the provider performs it.

## Why semantic execution?

A persistence model describes how data is stored.

A semantic model describes what an application is willing to expose and operate on.

Those models do not have to be identical.

For example, a persistence model might contain:

```text
Customer
 ├── Id
 ├── TenantId
 ├── Name
 ├── InternalRiskScore
 └── Accounts
```

An application-facing semantic model might expose:

```text
Customer
 ├── id
 ├── name
 └── accounts
      └── balance
```

The semantic surface can therefore be smaller, safer, and more purposeful than the physical model.

## Why this matters for AI

An AI agent is good at producing intent. It should not be trusted with unrestricted database authority.

A safer architecture is:

```text
AI
 ↓
structured intent
 ↓
Foundgine
 ├── resolve
 ├── validate
 ├── authorize
 ├── plan
 └── execute
 ↓
database
```

The application remains the authority over what the agent can do.

## Core concepts

Foundgine's public mental model can remain simple:

**Model → Request → Authorize → Plan → Execute → Result**

The deeper architecture adds metadata, expression trees, relationship traversal, rewriting, optimization, cost estimation, provider capabilities, and execution evidence.

## Where Foundgine fits

Foundgine can sit underneath interfaces rather than replacing them:

```text
REST/API ──────┐
GraphQL ───────┤
Automation ────┤
AI Agent ──────┤
               ▼
           Foundgine
               ▼
        SQL / InMemory / ...
```

This lets the interface and the execution model evolve independently.

---

## Getting Started

Run the `Foundgine.SupplyChain` sample end to end, then walk through it layer by layer — MCP boundary, application use cases, domain and AOT metadata, semantic model, and PostgreSQL execution. This follows GUIDE.md in the sample (`samples/Foundgine.SupplyChain/GUIDE.md`).

### What you'll run

The sample is a small supply-chain domain exposed to agents over MCP: customers, orders, order lines, products, suppliers, categories, inventory positions, warehouses, shipments and carriers.

```text
Agent / MCP client → Api → Application → Domain + Semantics → Foundgine Planning / Execution → Foundgine.Sql → PostgreSQL
```

It is deliberately separate from `benchmarks/AgentEndToEnd/SupplyChain`, which stays fixed as a benchmark workload while this sample evolves as the recommended reference architecture.

### 1. Prerequisites

.NET 9 SDK, Docker, and a clone of the Foundgine repository. The sample references the Foundgine `src/` projects directly rather than the published NuGet packages, so it builds against the exact source tree, including the current AOT source generator.

### 2. Start PostgreSQL

```powershell
cd samples/Foundgine.SupplyChain
docker compose up --build
```

PostgreSQL comes up on `localhost:4429`, the API container on `localhost:4422`.

### 3. Run the API

```powershell
dotnet run --project samples/Foundgine.SupplyChain/Api/Foundgine.SupplyChain.Api.csproj
```

MCP endpoint: `http://localhost:4422/mcp`. Health: `http://localhost:4422/health`. Exposed MCP tools: `describe_capabilities`, `get_my_orders`, `get_order`, `get_shipment`, `list_products`, `list_customers`, `get_product`, `get_inventory`, `list_suppliers`, `update_inventory`, `create_shipment`, `update_shipment`, `place_order`, `cancel_order`.

### 4. Walk the architecture layer by layer

1. **API layer (`Api`)** — thin ASP.NET + MCP host; no SQL, business rules, or semantic definitions.
2. **Application layer (`Application`)** — use-case boundary (`ISupplyChainQueries`/`ISupplyChainMutations`) with capability authorization in `SupplyChainApplication`.
3. **Domain layer (`Domain`)** — two separate representations: `*ERP` storage records (`FoundgineEntity`/`FoundgineField`/`FoundgineRelationship`) and application models (`Customer`, `SalesOrder`, ...), linked only via explicit `FoundgineConnection` declarations.
4. **AOT layer** — `Foundgine.Aot.Generator` compiles the AOT attributes into `Foundgine.Generated.GeneratedMetadata`, consumed via `SupplyChainSemanticModel.Metadata`.
5. **Semantic layer (`Semantics`)** — stable semantic entity/relationship IDs used instead of raw table names.
6. **Query repository (`Infrastructure/Queries`)** — builds semantic operations (`SemanticReadNode` → Planner → `SqlCompiler` → `SqlExecutionProvider`) rather than SQL strings.
7. **Mutation repository (`Infrastructure/Mutations`)** — simple mutations via `SemanticMutationBuilder` → `MutationPlanner` → `SqlMutationCompiler`.
8. **High-assurance mutations** — `place_order`/`cancel_order` keep explicit parameterized SQL for idempotency, advisory locks, `FOR UPDATE SKIP LOCKED`, and atomic multi-table invariants that are currently PostgreSQL-specific.
9. **MCP layer** — `SupplyChainMcpTools` are protocol adapters only, delegating to `SupplyChainApplication`.
10. **Testing layer (`Tests`)** — capability authorization → AOT metadata → semantic plans → SQL compilation → PostgreSQL integration → MCP contracts → full agent-facing E2E benchmark tests.

The key dependency rule: API → Application → Semantic intent → Foundgine planning → Provider — never API → SQL repository → PostgreSQL directly.

### 5. Next steps

`TUTORIAL.md` in the sample builds the same project from an empty solution step by step; `GUIDE.md` is the source for this section; the How It Works and Agent Benchmark pages below show execution evidence and a dedicated Supply Chain report.

---

## AI Agents with Foundgine


## Safe AI access to application data

The purpose of the Foundgine AI integration is not to make the model responsible for database access.

The intended boundary is:

```text
AI Agent
   ↓
Tool / structured intent
   ↓
Foundgine
   ↓
Authorization + planning
   ↓
Provider
   ↓
PostgreSQL
```

## The anti-pattern

Avoid:

```text
LLM
 ↓
generated SQL
 ↓
database credentials
 ↓
PostgreSQL
```

The model should not be the authority over database schema access, tenant isolation, or application authorization.

## The Foundgine pattern

```text
LLM
 │
 │ "Find customers with balances over $10k"
 ▼
Agent tool
 │
 │ structured request
 ▼
Foundgine
 ├── semantic resolution
 ├── validation
 ├── authorization
 ├── relationship traversal
 ├── planning
 └── provider execution
        │
        ▼
    PostgreSQL
```

## End-to-end proof target

The first E2E scenario should prove the complete chain:

1. An AI agent receives a natural-language task.
2. The model selects a Foundgine capability.
3. The capability produces structured intent.
4. Foundgine resolves the semantic model.
5. Authorization is evaluated.
6. An execution plan is produced.
7. PostgreSQL executes the plan.
8. The result returns to the agent.
9. Evidence is available for inspection.

## What this page describes vs. what exists today

Steps 3–7 above — structured intent, semantic resolution, authorization, planning, and provider execution — are the same core pipeline documented in the Architecture section; that pipeline is not specific to AI agents.

The semantic lifecycle itself is shipped and tested in 0.5.0. A general autonomous-agent runtime that owns model selection, orchestration, deployment infrastructure, and autonomous end-to-end behavior is not a current core guarantee.

## Security scenarios

The E2E suite should include at least:

### Allowed field

```text
Agent → customer.name
→ authorized
→ SQL
→ result
```

### Forbidden field

```text
Agent → customer.internalRiskScore
→ denied
→ no database execution
```

### Tenant isolation

```text
Agent → another tenant's customer
→ authorization predicate prevents access
```

### Prompt injection

A malicious value in application data must not become an instruction that changes the agent's authorization or Foundgine execution boundary.

## Deployment progression

Build the E2E in this order:

```text
1. Agent process
2. Foundgine
3. PostgreSQL
4. Deterministic E2E
5. HTTP boundary
6. Docker
7. Kubernetes
8. Terraform
9. CI/CD
```

The local deterministic test should prove the architecture before infrastructure is introduced.

---

## Foundgine Architecture


## Core execution pipeline

```text
Intent
  ↓
Semantic Model
  ↓
Resolution
  ↓
Authorization
  ↓
Plan
  ↓
Rewrite / Optimize
  ↓
Provider Compilation
  ↓
Execution
  ↓
Result + Evidence
```

## Separation of concerns

### Semantic model

Defines the application-facing capabilities.

### Intent

Describes what the caller wants without binding it directly to a physical provider.

### Authorization

Determines which parts of the requested operation are permitted and can contribute predicates or constraints to the execution plan.

### Planner

Builds a provider-independent representation of the requested operation.

### Rewriting and optimization

Transforms the plan while preserving semantics and authorization constraints.

### Provider

Compiles and executes the plan against a concrete backend.

## Multiple callers, one execution model

```text
REST/API
GraphQL
JSON
AI Agent
Automation
   │
   ▼
Semantic Intent
   │
   ▼
Foundgine Planner
   │
   ├── SQL
   ├── InMemory
   └── Future providers
```

The goal is to avoid implementing separate execution semantics for every interface.

## Why the intermediate plan matters

The plan is the architectural boundary between semantic intent and physical execution.

It gives the runtime a place to:

- preserve authorization constraints
- validate dependencies
- rewrite operations
- estimate cost
- reason about provider capabilities
- optimize execution
- produce execution evidence

This is the core mechanism that allows multiple input surfaces and providers to share execution semantics.

---

## How Foundgine Executes a Request

Two representative requests — a simple query with one relationship, and a relationship-heavy business process with four hops plus aggregation — are walked through every boundary the intent crosses, with representative payloads shown at each stage.

The two requests share the same pipeline. Watching them side by side makes one point concrete: intent changes into an authorized execution plan, never directly into SQL. The example payloads are illustrative JSON shapes meant to make the architecture legible; they are explanatory, not a promise of a public wire format.

What stays constant across both requests is the pipeline itself — the same semantic resolution, authorization, and planning stages handle a small lookup and a multi-hop operation without a separate code path for each.

---

## Agent Benchmark

Five end-to-end experiments ask one question: what should the agent execution boundary do? The problem is not giving an agent an API — it is giving the agent useful capabilities without making the API itself the execution model. As agent-facing capabilities multiply, a conventional endpoint-per-operation approach accumulates bespoke tools, repeated orchestration, duplicated policy checks, and increasingly large interaction traces.

Read the runs as a progression:

1. **Run 1 — Baseline.** Establishes whether semantic execution reduces agent work at all, on a single deterministic customer.
2. **Run 2 — Scalability.** Scales the same workflow across four customer tiers (10 to 10,000) and concurrency up to 64, to see whether agent-work reduction survives larger fixtures and separates from backend latency behavior.
3. **Run 3 — Efficiency.** Measures what happens when the workflow itself, not just the data volume, becomes larger.
4. **Run 4 — Execution architecture comparison.** Compares semantic MCP + Foundgine against a conventional GraphQL (Hot Chocolate) + EF Core agent path.
5. **Run 5 / 5b — High-assurance capability boundary.** Tests a high-assurance `TransferFunds` mutation through the same MCP protocol, then (5b) isolates individual MCP calls versus one semantic batch call using the exact same client.

The benchmark suite sits on top of a broader verification story: deterministic unit tests (semantic, planning, authorization, MCP, AOT, InMemory contracts), real PostgreSQL integration, penetration testing (authorization attacks, adversarial semantic input, hostile-model replay), performance gates, and a Supply Chain end-to-end report that carries an agent-like workload across MCP, semantic execution, authorization, ExecutionIR, and PostgreSQL. The CI release path requires the core verification gates before package publication; the Supply Chain E2E report adds the application-level proof on top of that.

Each run is measured on tool-call count, estimated context load, latency, and result correctness (same final state as the conventional path) — reducing agent interaction and reducing end-to-end latency are treated as independent claims, not assumed to move together.

---

## Foundgine Performance


## CoffeeBeanery PostgreSQL graph benchmark — 12 August 2026

Three independent successful runs were performed against a deterministic PostgreSQL graph workload.

### Workload

```text
Customer
  → CustomerBankingRelationship
      → Contract
          → Transaction
```

Fixture:

- 1,000 customers
- 4,000 relationships
- 12,000 contracts
- 48,000 transactions
- concurrency 1, 8, 32
- 10-second measurement per case
- 3-second warm-up
- 5-second request timeout

## Query result

At concurrency 32:

| Implementation | Average RPS | Average p95 |
|---|---:|---:|
| Conventional graph/API path | 139.4 | 338.4 ms |
| Foundgine — no cache | 2,781.0 | 20.3 ms |
| Foundgine — provider-plan cache | 2,838.9 | 19.9 ms |

That is approximately:

- 20.0× the throughput without the cache
- 20.4× with the cache
- 16.7× lower p95 latency without the cache
- 17.0× lower p95 latency with the cache

The large query advantage is therefore not dependent on provider-plan caching.

## Reliability

The three successful runs reported:

- 0 application errors
- 0 request timeouts
- 0 cancelled requests

## Mutation results

Mutation performance is more variable.

The benchmark supports the conclusion that Foundgine can perform well at higher concurrency, but mutation performance should not currently be presented as the primary performance claim.

## What this proves

The strongest evidence is for:

> **read/query execution over a relationship-heavy PostgreSQL graph workload.**

The results consistently show substantially higher query throughput and lower p95 latency in this controlled workload.

## What this does not prove

This is not a universal benchmark of every:

- EF Core workload
- PostgreSQL graph query workload
- PostgreSQL schema
- query shape
- mutation workload
- hardware configuration

Results depend on the workload, schema, provider versions, host, fixture, and implementation versions.

The appropriate claim is:

> **Foundgine demonstrates a substantial performance advantage for this relationship-heavy graph query workload.**
