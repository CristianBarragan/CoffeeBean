# Foundgine — full public context

## What Foundgine is

Foundgine is a programmable semantic execution platform for .NET. It creates a controlled boundary between application callers — APIs, GraphQL, automation, structured JSON, and AI agents — and the data and operations they are allowed to execute.

The core lifecycle is:

```text
Caller
  ↓
Structured intent
  ↓
Semantic resolution
  ↓
Authorization
  ↓
Provider-neutral plan
  ↓
Provider execution
  ↓
Result + evidence
```

Foundgine separates application meaning from physical persistence. A semantic model can expose a smaller, more purposeful surface than the underlying tables and columns.

## Why the boundary exists

Without a shared execution boundary, every caller can develop its own validation, authorization, query construction, and data-access path. Foundgine centralizes the application-facing semantic model so callers can converge on the same authorization and execution rules.

Foundgine is not an ORM replacement, database, GraphQL server, LLM, agent framework, identity provider, or general workflow engine. It is an execution layer that can sit underneath those systems.

## Architecture

Transport adapters and providers depend on Foundgine contracts; the semantic core does not depend on GraphQL, SQL, AI frameworks, or another transport/provider implementation.

```text
GraphQL ─┐
JSON ────┼──→ Semantic request → Plan → Provider
AI ──────┘                         ↙        ↘
                              SQL          InMemory
```

The intermediate plan provides a boundary for authorization preservation, dependency validation, rewriting, cost reasoning, provider capabilities, optimization, and execution evidence.

## Security boundary

Foundgine treats authorization as part of semantic execution rather than as a transport-only boolean check. Authorization predicates can be carried into the plan and must be preserved by provider execution.

For example:

```text
resource.TenantId == user.TenantId
```

The security model has three distinct concerns:

1. **Intent interpretation** — probabilistic and untrusted when produced by an AI model.
2. **Semantic execution** — deterministic once application meaning and policy are defined.
3. **Operational security** — authentication, identity management, secrets, transport security, rate limiting, database permissions, and deployment security remain application/infrastructure responsibilities.

Foundgine does not claim that an incorrect business policy becomes correct merely because it is authorized. A domain definition such as “available funds” must be defined correctly by the application.

## Adversarial intent

The repository includes tests for hostile structured intent and security-boundary regressions, including cross-tenant access, hidden-field selection, unauthorized relationship traversal, execution-control injection, bounded depth/fan-out/filter input, plan-level security invariants, and mutation replay.

## High-assurance mutations

The repository includes a `TransferFunds` example to exercise consequential mutation semantics:

```text
TransferFunds
  ↓
resolve source + destination
  ↓
verify tenant + ownership
  ↓
verify account state
  ↓
verify limits + available funds
  ↓
verify idempotency
  ↓
transactional execution
  ├── debit
  ├── credit
  └── audit
  ↓
execution receipt
```

The example distinguishes raw balance from available funds. Its semantic definition can account for pending transactions and regulatory holds.

The PostgreSQL implementation uses a transaction, deterministic account locking, current-state checks, database-backed idempotency, and audit persistence. This is a concrete proof of one consequential capability, not a claim that every business mutation is automatically safe.

## Providers

The SQL path lowers the provider-neutral execution model into relational SQL. The InMemory provider executes a limited subset directly over CLR-backed data and deliberately does not generate SQL. Provider independence means semantic meaning is not expressed as SQL; it does not mean every provider supports every operation.

A provider should declare supported operations, preserve required security invariants, compile logical operations into provider-native work, validate result semantics, and carry conformance/integration tests.

## AI agents

AI is an intent source, not a core dependency or authority over database access.

```text
AI interpretation
      ↓
structured intent
      ↓
semantic validation
      ↓
authorization
      ↓
plan
      ↓
provider
```

The host application owns `ExecutionContext`, so tenant identity, authorization context, provider selection, and database connection details do not become model-controlled tool arguments.

The project does not claim a general autonomous-agent runtime. The semantic agent boundary and tool integration are the important architecture; orchestration, model selection, deployment, and autonomous behavior remain application concerns.

## Performance evidence

The corrected 15 August 2026 CoffeeBeanery PostgreSQL benchmark compares Hot Chocolate + EF Core, Foundgine without provider-plan caching, and Foundgine with provider-plan caching.

Fixture:

- 1,000 customers
- 4,000 relationships
- 12,000 contracts
- 48,000 transactions
- concurrency 1/8/16/32/64
- mutation and upsert batch sizes 1/10/50

At concurrency 32 for the top-50 query:

| Target | RPS | p50 | p95 | p99 |
|---|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 224.0 | 141.8 ms | 187.0 ms | 216.4 ms |
| Foundgine — no cache | 2,352.2 | 12.5 ms | 25.2 ms | 34.2 ms |
| Foundgine — provider-plan cache | 2,975.7 | 9.9 ms | 18.4 ms | 23.0 ms |

The corrected report explicitly retracts an earlier contradictory table. The results are workload-specific engineering evidence, not a universal performance claim.

At concurrency 32, logical mutation throughput for batch sizes 1/10/50 was 781.4/5,793/12,990 for Foundgine without cache and 778.8/5,954/13,155 with cache, compared with 715.6/3,656/4,550 for Hot Chocolate + EF Core. The largest difference appears at larger batch sizes.

## Current validation status

Foundgine 0.3.0 is the current shipped release. The active repository contains semantic execution, authorization-aware planning, SQL and InMemory execution, GraphQL and JSON adapters, AOT metadata support, AI integration surfaces, execution evidence, and PostgreSQL integration infrastructure.

The latest supplied validation run was not green. It reported failures in semantic security/capability validation and an aggregate optimizer expectation. The JSON intent safety suite passed. PostgreSQL integration tests require a configured database connection and are environment-dependent.

Do not interpret historical milestone documents as current status or future commitments. The active source code and active tests are the source of truth.

## Public pages

- What is Foundgine: https://cristianbarragan.github.io/Foundgine/what-is-foundgine.html
- Architecture: https://cristianbarragan.github.io/Foundgine/architecture/
- Security: https://cristianbarragan.github.io/Foundgine/security/
- High-assurance mutations: https://cristianbarragan.github.io/Foundgine/mutations/
- Providers: https://cristianbarragan.github.io/Foundgine/providers/
- AI agents: https://cristianbarragan.github.io/Foundgine/ai-agents/
- Getting started: https://cristianbarragan.github.io/Foundgine/getting-started/
- Performance: https://cristianbarragan.github.io/Foundgine/performance/
- Repository: https://github.com/CristianBarragan/Foundgine
