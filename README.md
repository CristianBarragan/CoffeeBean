<div align="center">

# Coffee Beanery

**Model the business. Generate the execution.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-friendly-blue)](docs/10-Performance/Native-AOT.md)
[![HotChocolate](https://img.shields.io/badge/GraphQL-Hot%20Chocolate-e10098)](docs/05-GraphQL/README.md)
[![CodeQL](.github/workflows/badge.svg)](.github/workflows/codeql.yml)

</div>

---

> Coffee Beanery is a compile-time execution engine that transforms business models into
> deterministic execution plans, independent of transport, database, or infrastructure.
> **Everything else is an adapter.**

Coffee Beanery is **not** an ORM, a GraphQL framework, a workflow engine, or a database
abstraction layer. It's a compile-time execution engine: its one job is to turn business
intent — expressed as EF Core mappings — into a deterministic execution plan. Today that
plan is carried in by [Hot Chocolate](https://chillicream.com/docs/hotchocolate) (GraphQL),
executed against PostgreSQL, and run through [Dapper](https://github.com/DapperLib/Dapper).
None of those three are replaced — Coffee Beanery sits between them and the domain model.

**The application owns the business. Coffee Beanery owns the execution.**

```
                 Transport
        GraphQL   REST   gRPC
                │
                ▼
      Coffee Beanery Planner
                │
                ▼
         Execution Providers
      PostgreSQL  SQL Server
         Kafka     Temporal
         Redis      HTTP
                │
                ▼
          Infrastructure
```

Only the top-left and top-middle-left boxes are built today — see
[Phase 1 vs. future phases](docs/02-Architecture/Vision.md#roadmap-by-phase) below. The
diagram is the destination, not a claim about what ships now.

---

## Why Coffee Beanery

Traditional GraphQL-over-EF stacks discover their query shape, joins, and mappings at
request time, through reflection and expression-tree interpretation. Coffee Beanery moves
that discovery to **compile time**, using a Roslyn incremental source generator that reads
your EF Core mapping classes and emits the planner, materializers, and SQL-shaping metadata
your application runs against. The result:

- ✔ **Source-Generator Driven** — mapping metadata, planners, and materializers are emitted at compile time, not discovered via reflection at runtime.
- ✔ **Native AOT Friendly** — no runtime reflection on the request path; see [Native AOT](docs/10-Performance/Native-AOT.md).
- ✔ **GraphQL First (today)** — [Hot Chocolate](docs/05-GraphQL/README.md) is the Phase 1 transport; REST and gRPC are future adapters onto the same planner.
- ✔ **Deterministic Execution** — every request runs a known, generated execution plan rather than an interpreted one.
- ✔ **Provider-Based** — PostgreSQL + [Apache AGE](docs/08-Persistence/PostgreSQL-AGE.md) is the Phase 1 provider; the planner itself doesn't change when new providers are added.
- ✔ **CQRS-Shaped Runtime** — separate [query](docs/04-Runtime/Queries.md) and [mutation](docs/04-Runtime/Mutations.md) execution paths.
- ✔ **Dependency Injection Native** — see [Dependency Injection](docs/07-Dependency-Injection/README.md).

## Core Principles

**1. Business First** — the domain is the source of truth; infrastructure exists to serve it.
**2. Compile-Time by Default** — discover as much as possible during compilation; avoid runtime reflection and dynamic behavior whenever practical.
**3. Deterministic Execution** — every request executes through a known, generated execution plan.
**4. Provider-Based Architecture** — execution is delegated to providers; the planner doesn't change when a provider does.
**5. Transport Agnostic** — GraphQL, REST, and gRPC are all just ways of entering the same execution engine.

See [Principles](docs/02-Architecture/Principles.md) for the full, extended list.

---

## Quick Start

Coffee Beanery isn't published as a standalone NuGet package yet — Phase 1 projects
reference the source directly. The fastest way to see it running is the bundled sample:

```bash
git clone https://github.com/coffee-beanery/coffee-beanery.git
cd coffee-beanery/example/HotChocolateCoffeeBeanery

# Requires a local PostgreSQL instance with the Apache AGE extension —
# see docs/01-Getting-Started/Installation.md for the full setup.
dotnet build
dotnet run --project Api/Api.Banking
```

Then open the Banana Cake Pop GraphQL IDE at `http://localhost:4300/graphql` and run a
query against the sample Banking domain.

Full walkthrough: **[Getting Started → First Service](docs/01-Getting-Started/First-Service.md)**

---

## Documentation

Coffee Beanery's documentation is organized like a framework, not a folder of notes.
Start at the **[Documentation Home](docs/README.md)**, or jump straight to a section:

| Section | What's there |
|---|---|
| [01 · Getting Started](docs/01-Getting-Started/README.md) | Install, first service, configuration, FAQ |
| [02 · Architecture](docs/02-Architecture/README.md) | Vision, principles, layers, request pipeline, dependency graph |
| [03 · Foundation](docs/03-Foundation/README.md) | Contracts, metadata, extensibility |
| [04 · Runtime](docs/04-Runtime/README.md) | Execution, queries, mutations, events |
| [05 · GraphQL](docs/05-GraphQL/README.md) | Schema, resolvers, paging/filtering/sorting |
| [06 · Source Generators](docs/06-Source-Generators/README.md) | The mapping generator, diagnostics, pipeline stages |
| [07 · Dependency Injection](docs/07-Dependency-Injection/README.md) | Registration, lifetimes |
| [08 · Persistence](docs/08-Persistence/README.md) | PostgreSQL + AGE, Dapper/EF Core, caching |
| [09 · AI & LLM Readiness](docs/09-AI/README.md) | `llms.txt`, docs built for machine + human consumption |
| [10 · Performance](docs/10-Performance/README.md) | Native AOT, benchmarks |
| [11 · Samples](docs/11-Samples/README.md) | The bundled Banking sample |
| [12 · Contributing](docs/12-Contributing/README.md) | Workflow, code style, testing, ADR process |
| [13 · Reference](docs/13-Reference/README.md) | ADRs, FAQ, glossary, roadmap, changelog, migration guides |

## Sample Project

The [`example/HotChocolateCoffeeBeanery`](example/HotChocolateCoffeeBeanery) solution is a
Banking domain exercised end-to-end: EF Core mapping classes → generated planner → Hot
Chocolate GraphQL API → Dapper execution against PostgreSQL, with a graph read model on
Apache AGE. See [Samples](docs/11-Samples/README.md).

## Roadmap

Phase 1 is EF Core mapping + Hot Chocolate + PostgreSQL + Dapper. Everything past that —
additional providers, additional transports, additional infrastructure adapters — is
tracked as an extension of the same execution engine, not a change in direction. See the
full [Roadmap](docs/13-Reference/Roadmap.md).

## Contributing

Contributions are welcome — architecture proposals, generator improvements, provider
adapters, and documentation fixes alike. Start with
[Contributing](docs/12-Contributing/README.md) and the [Code Style](docs/12-Contributing/Code-Style.md)
guide, and see the [ADR process](docs/12-Contributing/ADR-Process.md) before proposing an
architectural change.

## License

Coffee Beanery is licensed under the [MIT License](LICENSE).
