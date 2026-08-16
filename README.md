<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs-site/assets/logo/foundgine-logo-dark.png">
  <img src="docs-site/assets/logo/foundgine-logo.png" alt="Foundgine" width="360">
</picture>

# [Foundgine.io](https://cristianbarragan.github.io/Foundgine/docs-site/index.html)

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

The security progression currently includes M17.3 security invariants, M17.4 plan-level proof, M17.5 SQL conformance, M17.6 high-assurance mutation conformance, and M17.7 cross-provider conformance.

## Foundgine and AI agents

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

See [`docs-site/performance/index.md`](docs-site/performance/index.md).

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

[`BENCHMARK-RESULTS-2026-08-12-CLEAN.md`](BENCHMARK-RESULTS-2026-08-12-CLEAN.md) is the source data behind [`docs-site/performance/index.md`](docs-site/performance/index.md); it's kept at the repo root as development/benchmark evidence rather than published-site content.

## Security

Foundgine's authorization and execution boundaries are intended to reduce unsafe access paths, but application security remains a shared responsibility. Authentication, secret management, transport security, rate limiting, database permissions, and deployment security remain application and infrastructure responsibilities.

## Status

Foundgine is actively evolving. Public API stability, provider coverage, AI-agent integrations, and production deployment patterns should be treated according to the project's current release and compatibility policy.

### M18.9 — Projection Pruning

Foundgine's planner includes a conservative projection-pruning rule that removes redundant duplicate fields without changing requested field order. Fields required by filters and ordering are tracked explicitly, and every accepted rewrite continues through semantic-equivalence and security-preservation proofs.

The current semantic model intentionally does not remove unique requested fields because output and working projections are not yet represented separately. That stronger dead-field optimization is reserved for a future projection-dependency milestone.


### M18.11 — Join Ordering / Multi-Relationship Planning

Adds conservative cardinality- and selectivity-aware traversal ordering metadata for sibling relationship plans. Logical child order remains unchanged; providers may use `TraversalOrder` for physical planning subject to semantic and security conformance.

