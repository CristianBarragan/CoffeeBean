# Foundgine 1.1.7 — Website full context

The public website explains the current architecture and deliberately excludes historical milestone/release material.


---

## what-is-foundgine.md

# What Is Foundgine?

Foundgine is a programmable semantic execution platform for .NET. It separates caller intent from application authority and physical execution.

## The problem

Applications increasingly have many callers: APIs, GraphQL, automation, internal services and AI agents. Without a common execution boundary, each caller can duplicate validation, authorization, orchestration and data access.

## The Foundgine model

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Resolution + Validation
  ↓
Authorization
  ↓
Provider-independent Plan
  ↓
Provider
  ↓
Result + Evidence
```

## Semantic versus persistence models

A persistence model describes storage. A semantic model describes what the application intentionally exposes. They can differ in fields, relationships, capabilities and authorization.

## Why it matters for AI

An AI model can propose structured intent without becoming the authority over database schema, tenants, credentials or business invariants. Foundgine re-evaluates the request inside the application-controlled semantic and authorization boundary.

## What Foundgine is not

Foundgine is not an ORM replacement, database, GraphQL server, identity provider, authorization server, workflow engine or general autonomous-agent framework.


---

## getting-started/index.md

# Get started with Foundgine

The canonical `Foundgine.SupplyChain` sample is the fastest way to understand the architecture in a real application.

## What you will run

```text
Agent / MCP client
      ↓
API
      ↓
Application capability
      ↓
Domain + AOT metadata
      ↓
Semantics
      ↓
Planning / ExecutionIR
      ↓
Foundgine.Sql
      ↓
PostgreSQL
```

## Prerequisites

- .NET 9 SDK
- Docker / Docker Compose
- a clone of the Foundgine repository

## Start PostgreSQL

Use the repository's supplied PostgreSQL Compose configuration.

```bash
docker compose -f docker-compose.postgres.yml up -d
```

## Run the sample

The exact command and configuration are maintained in `samples/Foundgine.SupplyChain/GUIDE.md`. The important part of the exercise is following one request through the layers rather than memorizing a command sequence.

## Layer-by-layer

### API

Transport handling only. It should not construct SQL or become the authorization authority.

### Application

Business capabilities and use-case orchestration. This is where application ownership of the operation remains visible.

### Domain

Domain types and business concepts.

### AOT metadata

`Foundgine.Aot.Generator` turns compile-time declarations into generated metadata, reducing runtime discovery and supporting Native AOT-friendly applications.

### Semantics

Structural metadata becomes application meaning: semantic entities, fields, relationships, capabilities and authorization.

### Planning

Semantic operations become provider-independent plans and `ExecutionIR`. Physical SQL is not part of this layer.

### Execution / provider

`Foundgine.Execution` owns the final execution boundary. `Foundgine.Sql` lowers the work to parameterized SQL and executes it through ADO.NET/PostgreSQL.

### MCP

MCP exposes capabilities to an external caller. It remains an adapter; host-owned identity and authorization stay outside the protocol.

### Testing

The repository tests each seam independently, then composes them into PostgreSQL and end-to-end scenarios.

## Next

Read [How it works](../how-it-works/index.html), then [Architecture](../architecture/index.html), and finally the [advanced semantic sample](../samples/semantic/index.html).


---

## ai-agents/index.md

# AI Agents with Foundgine

Foundgine gives an AI agent a controlled application capability surface without giving the model database authority.

## Intended boundary

```text
AI agent
  ↓
capability discovery / structured intent
  ↓
Foundgine
  ├─ resolve
  ├─ validate
  ├─ authorize
  ├─ plan
  └─ execute
  ↓
provider
```

## Capability discovery is not authorization

Capability descriptions help a model construct valid intent. The server resolves and authorizes every actual request again.

## Security

Authentication, identity, tenant context and model orchestration remain host responsibilities. Foundgine enforces semantic authorization and preserves security constraints into planning/execution.

## Foundgine.AI

`Foundgine.AI` integrates with `Microsoft.Extensions.AI`, exposing Foundgine operations as model tools without hard-coding a model provider.

## What is outside the core guarantee

Foundgine is not a general autonomous-agent framework. Model selection, memory, orchestration, deployment and autonomous behavior belong to the surrounding application.


---

## architecture/index.md

# Foundgine Architecture

## Core pipeline

```text
Intent → Resolve → Authorize → Plan → Rewrite/Optimize → Provider Compilation → Execution → Result + Evidence
```

## Semantic model

Application-facing meaning: entities, fields, relationships, capabilities and authorization. It is provider-independent.

## Metadata

Structural facts can be discovered from application declarations and AOT-generated metadata without making semantic code depend on runtime reflection.

## Planning

`Foundgine.Planning` produces provider-independent plans and `ExecutionIR`. Logical filters, ordering, pagination, traversal and aggregation stay logical; physical execution choices belong to providers.

## Execution

`Foundgine.Execution` is the provider boundary for compilation/dispatch, security conformance, materialization and execution evidence.

## Providers

`Foundgine.Sql` provides SQL/PostgreSQL execution. `Foundgine.InMemory` provides a small non-SQL implementation for provider-independence testing.

## Adapters

JSON, GraphQL, MCP and AI integrations translate caller requests into Foundgine operations. They do not become the authority over execution.


---

## performance/index.md

# Performance and benchmark evidence

Foundgine performance claims are scoped to explicit workloads. The benchmark suite separates measured RPS, latency, tool calls and success/failure counts from estimated context metrics.

The strongest current agent-facing evidence concerns reduced tool coordination and semantic batching. The TransferFunds run intentionally records a concurrency limitation rather than hiding it; the same-client follow-up isolates request shape and demonstrates the benefit of one semantic batch call.

PostgreSQL query measurements are also workload-specific and should not be treated as a universal comparison against every ORM, schema or hardware configuration.


---

## samples/semantic/authorization.md

# SupplyChain semantic authorization cases

The SupplyChain sample is deliberately a mixed authorization laboratory. It demonstrates six independent policy boundaries and exercises them through an MCP client that treats every request as untrusted.

## 1. Entity policy

Entity policy answers whether the semantic resource itself is available. `ComplianceIncident` is visible to analysts and supply-chain managers, but not customers.

## 2. Field policy

Field policy narrows an otherwise readable entity. `InventoryLot.Quarantined` is operationally sensitive and `Supplier.RiskScore` is restricted to analyst/manager roles.

## 3. Relationship policy

Relationship policy controls traversal. Even if the source entity is readable, a denied relationship removes the child subtree. `Supplier.incidents` is restricted.

## 4. Conditional policy

Tenant-owned resources use a provider-independent predicate:

```text
resource.TenantId == context.TenantId
```

The predicate is semantic IR and must survive planning and provider lowering. The caller cannot replace it with a predicate supplied in the request.

## 5. Write policy

Writes are opt-in. A role that can read an entity is not automatically allowed to mutate it. Inventory writes require an operational role in this sample.

## 6. Named operation policy

Coarse write access can be refined by a domain operation name. `inventory.reconcile` is manager-only even though a warehouse operator may perform ordinary inventory updates.

## Capability discovery

`describe_capabilities` exposes a safe description of allowed, denied and conditional capabilities. It is not a credential. The server re-evaluates the policy for every actual tool call.

## 7. Client-supplied claims

`read_entity`, `write_entity`, and `policy_probe` accept an optional, untrusted `claims` dictionary from the caller itself, separate from the server-derived `actor`/`token` identity. A fail-closed `ClientClaimsValidator` is the only path a claim can take into the policy:

- Reserved identity keys (`role`, `tenant`, `tenantId`, `actor`, `isAdmin`, `admin`, `permissions`, `capabilities`, `scopes`) are never accepted — presence alone fails the whole request closed, even if the value matches reality.
- Recognized keys (`scope`, `warehouse`, `max_rows`, `reason`, `change_ticket`, `not_after`) are validated per-key; a malformed value is rejected individually.
- Unrecognized keys are dropped individually and reported back, without blocking the rest of the call.
- Evidence (`reason`, `change_ticket`) paired with an expired `not_after` is rejected as stale.

Only the accepted claims ever reach `SupplyChainAuthorizationPolicy`, and each one can only narrow what the role already allows: `scope=read-only` self-restricts writes for that call, `warehouse=<id>` ANDs an extra resource predicate onto the tenant predicate, and `reason`/`change_ticket` add a required evidence gate on top of the existing manager-only check for `inventory.reconcile`. Nothing a claim asserts can widen access.

## MCP adversarial matrix

| Attempt | Expected |
|---|---|
| Cross-tenant read | Denied / conditional predicate retained |
| Restricted field | Denied |
| Restricted relationship | Denied |
| Analyst mutation | Denied |
| Operator `inventory.reconcile` | Denied |
| Customer inventory write | Denied |
| Authorized operator inventory update | Allowed |
| Claim: `role`/`tenant` injection | Denied — call fails closed |
| Claim: missing/malformed/expired reconcile evidence | Denied |
| Claim: self-imposed `scope=read-only` | Allowed — honored, restricts the call |
| Claim: `warehouse=<id>` scoping | Allowed — honored, narrows the result set |
| Claim: unrecognized key | Allowed — dropped individually, call proceeds |
| Claim: valid reconcile evidence | Allowed — honored alongside the role check |

The client is intentionally protocol-level and small so the security demonstration does not depend on a model provider. It is an adversarial caller, not a trusted test harness.


---

## packages/index.md

# Foundgine packages

The website package catalog is generated from the current source package boundaries. See `index.html` for the complete interactive/static page.


---

## Package catalog

The complete package map is available at `/packages/`; every source package also has a package-level README under `src/`.

---

## security/index.md

# Foundgine Security

Foundgine treats intent as untrusted and carries authorization constraints into planning and provider execution. Authentication and identity lifecycle remain host-owned.

## Invariant

```text
Intent → Resolve → Authorize → Security-preserving Plan → Provider Conformance → Execute
```

Capability discovery is descriptive, not authorization. Caller-supplied claims cannot widen authority. Optional `Foundgine.Security.Authority` infrastructure is outside the core execution boundary.
