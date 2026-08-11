# Foundgine

Foundgine is a small, provider-independent semantic execution engine for .NET.

It is designed to help developers bridge the gap between their existing codebases and AI agents. It allows you to take your existing domain model (the classes and logic that define your application's data and rules) and turn them into a safe, semantic interface. This makes it easier for AI agents to understand, plan, execute, and verify operations within your .NET application.

```text
API / Intent
    ↓
Semantic Request
    ↓
Resolution + Authorization
    ↓
Execution Plan
    ↓
Provider
    ↓
Data
```

The core does not know about GraphQL, SQL, EF Core, or a database. Those concerns live at the edges.

## Why it exists

The motivation behind tools like Foundgine comes from the specific failure modes of LLMs when interacting with complex enterprise codebases:

Safety & Invariants (State Corruption): LLMs don't inherently understand database constraints, multi-step transaction lifecycles, or business rules. If an LLM calls OrderService.Cancel() followed by PaymentService.Refund(), but hallucinates the parameter sequence or skips prerequisite validation, it can leave the system in an inconsistent state. An application runtime wraps operations in strict domain contracts so agents can only execute valid transitions.

Context Window Saturation: Real-world enterprise APIs often have hundreds of endpoints with thousands of parameters. Dumping raw OpenAPI specs or C# types into a prompt wastes tokens, causes hallucinations, and degrades reasoning. A semantic runtime exposes a curated interface tailored for planning.

Execution, Verification & Rollback: When an agent runs a multi-step plan, a runtime can simulate/dry-run the plan, verify post-conditions, and manage rollback or compensation transactions if an intermediate step fails.

Auditability & Determinism: Enterprise systems require strict logging of why an action was taken, which model initiated it, and what safeguards were evaluated.

Foundgine is useful when an application needs:

- relationship-aware requests;
- authorization before planning;
- more than one input format;
- provider-independent plans;
- deterministic domain metadata;
- a safe boundary for structured AI intent.

It is **not** an ORM, GraphQL server, database, workflow engine, or agent framework.

## Current proof

The repository currently proves the pipeline with:

- semantic domain modelling and resolution;
- authorization;
- provider-independent query and mutation planning;
- SQL execution against SQLite;
- AOT metadata generation;
- JSON intent input;
- a Hot Chocolate GraphQL adapter, including queries and mutations.

The Banking tests provide the main end-to-end proof.

## Projects

| Project | Purpose |
|---|---|
| `Foundgine.Abstractions` | Stable cross-layer contracts and IDs |
| `Foundgine.Metadata` | Domain and storage metadata |
| `Foundgine.Semantics` | Semantic model, requests, resolution, authorization |
| `Foundgine.Planning` | Provider-independent plans |
| `Foundgine.Execution` | Execution contracts and result materialization |
| `Foundgine.Sql` | SQL provider |
| `Foundgine.Aot` | AOT metadata attributes/contracts |
| `Foundgine.Aot.Generator` | Roslyn metadata generator |
| `Foundgine.Intent.Json` | JSON intent adapter |
| `Foundgine.GraphQL.HotChocolate` | GraphQL query/schema adapter |
| `Foundgine.GraphQL.HotChocolate.Mutations` | GraphQL mutation adapter |

## Documentation

- [Getting started](docs/GETTING-STARTED.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Current status](docs/CURRENT-STATUS.md)
- [GraphQL](docs/GRAPHQL.md)
- [Runtime](docs/RUNTIME.md)
- [AOT](docs/AOT.md)
- [Testing](docs/TESTING.md)
- [Security](docs/SECURITY.md)
- [Roadmap](docs/ROADMAP.md)
- [History](docs/history/README.md)

For AI/search context, see [`ai.seo.md`](ai.seo.md) and [`llms.txt`](llms.txt).

## Build

```powershell
dotnet test
```

The test suite is the source of truth for what is currently proven.
