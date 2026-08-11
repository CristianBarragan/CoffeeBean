# Foundgine

Foundgine is a small, provider-independent semantic execution engine for .NET.

It sits between an application's domain model and its data providers:

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
