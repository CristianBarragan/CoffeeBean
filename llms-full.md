# Foundgine 1.1.9

Foundgine is a **programmable semantic execution platform for .NET**. It creates an application-controlled boundary between structured caller intent and physical execution.

## Canonical lifecycle

```plantuml
@startuml
start
:Caller → Intent → Semantic Model → Semantic Operation Graph → Retrieval → Resolution → Authorization → Plan Binding → Execution IR → Provider → Execution → Evidence;
stop
@enduml
```

Retrieval is candidate discovery, not authorization.

## Core vocabulary

- **Semantic model** — application-defined meaning: entities, fields, identities, relationships, aliases and capabilities.
- **Intent** — what the caller requests, independently of physical SQL/provider operations.
- **Retrieval** — candidate discovery plus evidence; never an authorization decision.
- **Resolution** — selects and validates the intended semantic meaning.
- **Authorization** — determines what the current trusted execution context may exercise.
- **Plan** — provider-independent logical execution.
- **ExecutionIR** — the controlled intermediate execution representation at the provider boundary.
- **Provider** — physical execution implementation.
- **Evidence** — execution/security/plan context such as receipts and fingerprints.

## Adapters and providers

Intent can originate from application code, JSON, GraphQL, MCP or AI tools. These are adapters/consumers of the semantic boundary.

Current providers include `Foundgine.Sql` for SQL/PostgreSQL and a deliberately limited `Foundgine.InMemory` provider. PostgreSQL retrieval includes relational lookup, `pg_trgm`, native full text, optional `pg_search`/BM25 and optional Apache AGE graph similarity. Vector retrieval is not currently implemented.

## AI

AI may generate structured intent. It does not decide application authority, tenant identity, exposed semantics, credentials, policy or whether security invariants may be skipped.

```plantuml
@startuml
start
:AI;
:semantic capability / intent;
:Foundgine;
:resolve;
:authorize;
:plan;
:provider;
stop
@enduml
```

## MCP

`Foundgine.MCP` is a transport/capability adapter. Discovery is advisory. The host supplies trusted security context and every actual request is resolved and authorized.

## Security

The core security boundary is:

```plantuml
@startuml
start
:untrusted input;
:semantic resolution;
:authorization;
:security-preserving plan;
:provider conformance;
:execution;
stop
@enduml
```

Identity, tenant, audience, secrets and other authority remain host-owned.

## Current evidence

The `Foundgine.SupplyChain` sample and AgentEndToEnd benchmark demonstrate an agent-facing business workload through MCP, semantic execution, authorization, ExecutionIR and PostgreSQL. The associated PenTest suite contains 14 deterministic cases: 7 MCP and 7 GraphQL. Benchmark pages separate measured performance from modeled efficiency estimates.

## Packages

- `Foundgine` — runtime facade
- `Foundgine.Abstractions` — stable contracts and identifiers
- `Foundgine.Metadata` — structural metadata
- `Foundgine.Semantics` — meaning, intent, resolution, authorization
- `Foundgine.Planning` — provider-independent planning and safe rewrites
- `Foundgine.Execution` — ExecutionIR, provider boundary, results and evidence
- `Foundgine.Sql` — SQL/PostgreSQL provider
- `Foundgine.InMemory` — limited non-SQL provider
- `Foundgine.Aot` / `Foundgine.Aot.Generator` — AOT declarations and source generation
- `Foundgine.Intent.Json` — JSON intent adapter
- `Foundgine.GraphQL.HotChocolate*` — GraphQL adapters/execution boundaries
- `Foundgine.MCP` — MCP adapter
- `Foundgine.AI` — `Microsoft.Extensions.AI` integration
- `Foundgine.Security.Authority` — optional authority/recovery control-plane infrastructure

## Current release

**1.1.9 · .NET 9**

For implementation truth, use the active source tree, tests, `docs/CURRENT-STATUS.md`, and package READMEs.

## Website navigation

- `/` — Foundgine landing page
- `/case-studies/supply-chain/` — featured Supply Chain case study with capabilities and benchmark evidence
- `/agent-benchmark/supply-chain/` — live/published Supply Chain E2E benchmark report
- `/samples/semantic/` — advanced semantic execution sample
- `/samples/pentest/` — security penetration-test sample
- `/architecture/` — architecture and lifecycle
- `/ai-agents/` — AI/agent boundary
- `/security/` — security model
- `/packages/` — package map
