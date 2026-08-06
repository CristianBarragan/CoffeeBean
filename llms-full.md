# Coffee Beanery — Full Documentation

---

> Generated from `docs/`. This is a single-file concatenation of the full documentation set for LLM tools that prefer one ingest target over crawling links — see `docs/09-AI/LLM-Readiness.md`. Humans should read `docs/README.md` instead; it has working navigation.

---

<div align="center">

# Coffee Beanery

**Model the business. Generate the execution.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-friendly-blue)](docs/10-Performance/Native-AOT.md)
[![HotChocolate](https://img.shields.io/badge/GraphQL-Hot%20Chocolate-e10098)](docs/05-GraphQL/README.md)
[![CodeQL](https://github.com/coffee-beanery/coffee-beanery/actions/workflows/codeql.yml/badge.svg)](.github/workflows/codeql.yml)

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

---

[Home](../README.md) → **Documentation**

# Coffee Beanery Documentation

> Coffee Beanery is a compile-time execution engine that transforms business models into
> deterministic execution plans, independent of transport, database, or infrastructure.
> Everything else is an adapter.

Coffee Beanery is not an ORM, a GraphQL framework, a workflow engine, or a database
abstraction layer. It's a compile-time execution engine: it turns EF Core mapping classes
into a deterministic, generated execution plan. Phase 1 wires that plan to Hot Chocolate
(GraphQL) as the transport, PostgreSQL as the execution provider, and Dapper as the SQL
executor — see [Vision](02-Architecture/Vision.md) for what's built today versus what's
on the roadmap.

---

## Contents

| # | Section | Description |
|---|---|---|
| 01 | [Getting Started](01-Getting-Started/README.md) | Install Coffee Beanery, stand up your first service, configure it |
| 02 | [Architecture](02-Architecture/README.md) | Vision, principles, layers, request pipeline, dependency graph |
| 03 | [Foundation](03-Foundation/README.md) | The dependency-free contract layer everything else builds on |
| 04 | [Runtime](04-Runtime/README.md) | Execution, query planning, mutation planning, events |
| 05 | [GraphQL](05-GraphQL/README.md) | Schema, resolvers, paging/filtering/sorting via Hot Chocolate |
| 06 | [Source Generators](06-Source-Generators/README.md) | The Roslyn incremental generator that replaces runtime reflection |
| 07 | [Dependency Injection](07-Dependency-Injection/README.md) | Composition root, registration, lifetimes |
| 08 | [Persistence](08-Persistence/README.md) | PostgreSQL + Apache AGE, Dapper/EF Core, caching |
| 09 | [AI & LLM Readiness](09-AI/README.md) | `llms.txt`, structured docs for machine consumption |
| 10 | [Performance](10-Performance/README.md) | Native AOT design, benchmark results |
| 11 | [Samples](11-Samples/README.md) | The bundled Banking sample walkthrough |
| 12 | [Contributing](12-Contributing/README.md) | Workflow, code style, testing strategy, ADR process |
| 13 | [Reference](13-Reference/README.md) | ADRs, FAQ, glossary, roadmap, changelog, migration guides |

Full navigation: **[SUMMARY.md](SUMMARY.md)**

---

## Where to start

- **New to Coffee Beanery?** Start at [Getting Started](01-Getting-Started/README.md).
- **Evaluating the architecture?** Start at [Architecture → Vision](02-Architecture/Vision.md).
- **Contributing code?** Start at [Contributing](12-Contributing/README.md).
- **Looking for a specific decision or term?** Start at [Reference](13-Reference/README.md).

---

## Related Documentation

- [Project Home](../README.md)
- [Architecture](02-Architecture/README.md)
- [Reference → ADRs](13-Reference/ADRs.md)
- [Reference → Roadmap](13-Reference/Roadmap.md)

---

Next: [Getting Started](01-Getting-Started/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Getting Started**

# Getting Started

This section gets a Coffee Beanery-backed service running locally and explains the moving
parts you just started. If you want the philosophy first, see
[Architecture → Vision](../02-Architecture/Vision.md).

---

## Contents

- [Installation](Installation.md) — prerequisites, PostgreSQL + Apache AGE setup, cloning the repo
- [First Service](First-Service.md) — running the sample and understanding the request path
- [Configuration](Configuration.md) — connection strings, DI registration, appsettings
- [FAQ](FAQ.md) — the questions people ask in the first hour

---

## The shortest possible path

```bash
git clone https://github.com/coffee-beanery/coffee-beanery.git
cd coffee-beanery/example/HotChocolateCoffeeBeanery
dotnet build
dotnet run --project Api/Api.Banking
```

That assumes PostgreSQL with Apache AGE is already reachable at the connection string in
`appsettings.json`. If it isn't yet, start with [Installation](Installation.md).

---

## Related Documentation

- [Architecture](../02-Architecture/README.md)
- [Samples](../11-Samples/README.md)
- [Reference → FAQ](../13-Reference/FAQ.md)

---

← Previous: [Documentation Home](../README.md)  |  Next: [Architecture](../02-Architecture/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **Configuration**

# Configuration

## Contents

- [Connection strings](#connection-strings)
- [Kestrel and hosting](#kestrel-and-hosting)
- [Dependency injection wiring](#dependency-injection-wiring)
- [Startup warmup](#startup-warmup)

---

## Connection strings

Coffee Beanery reads standard ASP.NET Core configuration. The sample's
`appsettings.json` looks like:

```json
{
  "ConnectionStrings": {
    "BankingConnectionString": "Host=localhost:5432;Database=BankingDB;Username=sa;Password=123456"
  },
  "Kestrel": {
    "EndPoints": { "Http": { "Url": "http://localhost:4300" } }
  }
}
```

Replace credentials and host for your environment. Multiple bounded contexts can register
their own connection strings and their own `Database.Entity.*` / `Database.Graph.*` projects,
following the same pattern as `Database.Entity.Banking` / `Database.Graph.Banking`.

## Kestrel and hosting

The sample hosts Hot Chocolate over standard Kestrel/ASP.NET Core. Nothing about Coffee
Beanery's runtime requires a specific host model — see
[Architecture → Layers](../02-Architecture/Layers.md) for how GraphQL is treated as a
transport adapter rather than a hosting requirement.

## Dependency injection wiring

Registration follows a composition-root pattern: Foundation contracts, generated
registrations, runtime services, and the SQL/PostgreSQL provider are each registered in
their own extension method, called from `Program.cs`. See
[Dependency Injection → Registration](../07-Dependency-Injection/Registration.md) for the
full breakdown and lifetime guidance.

## Startup warmup

Before the first request is served, the runtime executes a warmup pass (`GraphWarmup.Init`)
that discovers all `IMappingSet` implementations, pre-resolves reflection-derived property
info, compiles getter/setter delegates, and pre-builds the node traversal tree — so no
reflection work is left on the request path. See
[Performance → Benchmarks](../10-Performance/Benchmarks.md#why-response-times-are-this-low)
for the mechanics and why it matters.

---

## Related Documentation

- [Dependency Injection](../07-Dependency-Injection/README.md)
- [Persistence](../08-Persistence/README.md)
- [Performance](../10-Performance/README.md)

---

← Previous: [First Service](First-Service.md)  |  Next: [FAQ](FAQ.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **FAQ**

# Getting Started FAQ

## Contents

- [Do I need to learn a new modeling API?](#do-i-need-to-learn-a-new-modeling-api)
- [Does this replace Hot Chocolate or Dapper?](#does-this-replace-hot-chocolate-or-dapper)
- [Is this production-ready?](#is-this-production-ready)
- [Where's the full FAQ?](#wheres-the-full-faq)

---

## Do I need to learn a new modeling API?

No. Phase 1 uses **EF Core mapping classes** as the metadata source — see
[First Service → Write a mapping class](First-Service.md#write-a-mapping-class). Coffee
Beanery reads that mapping at compile time; it doesn't ask you to learn a parallel schema
language.

## Does this replace Hot Chocolate or Dapper?

No — it deliberately doesn't. Hot Chocolate remains the GraphQL framework, Dapper remains
the SQL executor. Coffee Beanery sits between your domain model and those tools, generating
the execution plan that connects them. See
[Architecture → Vision](../02-Architecture/Vision.md#what-coffee-beanery-is) for the full
positioning.

## Is this production-ready?

Treat it as early-stage. The mapping source generator is explicitly marked
**"not yet build-verified"** against arbitrary mapping shapes beyond the sample — see
[Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md#known-risk-areas).
Review the [Roadmap](../13-Reference/Roadmap.md) and [ADRs](../13-Reference/ADRs.md) before
depending on it for anything load-bearing.

## Where's the full FAQ?

The extended architecture FAQ — covering source generators vs. reflection, transport and
provider independence, and Native AOT — lives in
**[Reference → FAQ](../13-Reference/FAQ.md)**.

---

## Related Documentation

- [Reference → FAQ](../13-Reference/FAQ.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)
- [Reference → ADRs](../13-Reference/ADRs.md)

---

← Previous: [Configuration](Configuration.md)  |  Next: [Architecture](../02-Architecture/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **First Service**

# Your First Service

## Contents

- [What you're running](#what-youre-running)
- [Anatomy of the sample](#anatomy-of-the-sample)
- [Write a mapping class](#write-a-mapping-class)
- [Run a query](#run-a-query)
- [What just happened](#what-just-happened)

---

## What you're running

The bundled sample, `example/HotChocolateCoffeeBeanery`, models a small Banking domain:
customers, accounts, contracts, and transactions. It's wired end to end through Coffee
Beanery's Phase 1 stack:

```
EF Core mapping classes  →  generated execution plan  →  Hot Chocolate (GraphQL)  →  Dapper  →  PostgreSQL
```

## Anatomy of the sample

| Project | Role |
|---|---|
| `Api/Api.Banking` | The ASP.NET Core host — Hot Chocolate endpoint, query/mutation resolvers |
| `Domain/CoffeeBeanery` | The framework runtime, wired into this specific solution |
| `Domain/CoffeeBeanery.GraphQL.Core.Foundation` | Foundation contracts (see [Foundation](../03-Foundation/README.md)) |
| `Domain/CoffeeBeanery.GraphQL.Core.Mapping.Generators` | The Roslyn source generator (see [Source Generators](../06-Source-Generators/README.md)) |
| `Domain/Domain.Model`, `Domain/Domain.Shared` | The business/domain model and shared mapping DSL |
| `Infrastructure/Database/*` | EF Core entity models, migrations, and the PostgreSQL/AGE providers |

## Write a mapping class

Business models are mapped to storage entities in a `partial` mapping class that derives
from `BaseModelMappingRegistration<T>`. `BuildMap()` is read by the generator at compile
time — it's the source of truth the generator parses, not code that runs at request time:

```csharp
public partial class ProductMapping : BaseModelMappingRegistration<Product>
{
    public ProductMapping() : base(alias: "product", modelName: nameof(Product)) { }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap { /* ... */ };
        map.AddModelToEntity<Product, ProductEntity>();
        map.FieldMaps.Add(new FieldMap { /* ... */ });
        return map;
    }
}
```

The generator emits the other half of the `partial class` — a compiled `Register()` override
that builds the node tree directly, with no reflection at runtime. See
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md) for the
full mechanics and the required base-class shape.

## Run a query

With the sample running (see [Installation](Installation.md)), open the GraphQL IDE and run:

```graphql
query {
  customers(first: 5) {
    nodes {
      id
      name
      accounts {
        nodes { id balance }
      }
    }
  }
}
```

## What just happened

1. Hot Chocolate parsed the GraphQL request and handed it to Coffee Beanery's [runtime](../04-Runtime/README.md).
2. The generated [query planner](../04-Runtime/Queries.md) resolved the requested fields against
   compile-time metadata — no reflection, no runtime type discovery.
3. A single batched SQL statement was built and executed via Dapper against PostgreSQL.
4. Rows were mapped back to domain models using pre-compiled delegates (see
   [Performance → Benchmarks](../10-Performance/Benchmarks.md) for why this step has no
   reflection cost).
5. Hot Chocolate serialized the result graph back to the client.

Continue to [Configuration](Configuration.md) to see how connection strings, DI, and warmup
are wired together.

---

## Related Documentation

- [Configuration](Configuration.md)
- [Runtime](../04-Runtime/README.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Samples](../11-Samples/README.md)

---

← Previous: [Installation](Installation.md)  |  Next: [Configuration](Configuration.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **Installation**

# Installation

## Contents

- [Prerequisites](#prerequisites)
- [Clone the repository](#clone-the-repository)
- [Set up PostgreSQL and Apache AGE](#set-up-postgresql-and-apache-age)
- [Restore and build](#restore-and-build)
- [Verify](#verify)

---

## Prerequisites

Coffee Beanery targets **.NET 9** (`net9.0`) and is built and tested on x64. You'll need:

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- **PostgreSQL** with the **Apache AGE** extension enabled (used for the graph read path — see [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md))
- Optionally, a local Redis or FasterKV-compatible cache — the sample uses `FasterKv.Cache.Core` in-process, so no external cache server is required to get started

Coffee Beanery's runtime package pulls in Hot Chocolate, Dapper.Contrib, EF Core (design-time,
for mapping metadata), Npgsql, AutoMapper, and Z.Dapper.Plus. You don't need to install these
separately — `dotnet restore` handles it.

## Clone the repository

```bash
git clone https://github.com/coffee-beanery/coffee-beanery.git
cd coffee-beanery
```

The repository has two top-level trees:

- `src/CoffeeBeanery` — the framework itself
- `example/HotChocolateCoffeeBeanery` — a full sample application (Banking domain) that
  exercises the framework end to end

## Set up PostgreSQL and Apache AGE

1. Provision a PostgreSQL instance (local Docker container or a managed instance).
2. Install and enable the [Apache AGE](https://age.apache.org/) extension on the target database.
3. Create the database referenced by the sample's connection string (`BankingDB` by default —
   see `example/HotChocolateCoffeeBeanery/Api/Api.Banking/appsettings.json`).
4. Apply the EF Core migrations under `Infrastructure/Database/Database.Entity.Banking/Migrations`
   and `Infrastructure/Database/Database.Graph.Banking/Migrations`.

Update `ConnectionStrings:BankingConnectionString` in `appsettings.json` to point at your instance.

## Restore and build

```bash
cd example/HotChocolateCoffeeBeanery
dotnet restore
dotnet build
```

The build triggers the [mapping source generator](../06-Source-Generators/Mapping-Generator.md),
which reads your EF Core mapping classes and emits the compile-time execution plan. If the
generator reports a diagnostic (`CBMAP00x`), see
[Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md) before continuing.

## Verify

```bash
dotnet run --project Api/Api.Banking
```

Open `http://localhost:4300/graphql` (the port is set in `appsettings.json` under `Kestrel`)
to reach the Banana Cake Pop GraphQL IDE. If the schema loads and you can run an introspection
query, installation succeeded — continue to [First Service](First-Service.md).

---

## Related Documentation

- [First Service](First-Service.md)
- [Configuration](Configuration.md)
- [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md)

---

← Previous: [Getting Started](README.md)  |  Next: [First Service](First-Service.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Architecture**

# Architecture

Coffee Beanery is a compile-time execution engine. This section explains the vision, the
principles that constrain every design decision, how the codebase is layered, how a request
actually flows through the system, and how the projects depend on one another.

---

## Contents

- [Vision](Vision.md) — the bold statement, the mission, and Phase 1 vs. future phases
- [Principles](Principles.md) — the five core principles, plus the extended engineering principles
- [Layers](Layers.md) — how the solution is organized and what each project owns
- [Request Pipeline](Request-Pipeline.md) — a request, traced end to end
- [Dependency Graph](Dependency-Graph.md) — allowed dependency directions between projects

---

## Related Documentation

- [Foundation](../03-Foundation/README.md)
- [Runtime](../04-Runtime/README.md)
- [Reference → ADRs](../13-Reference/ADRs.md)

---

← Previous: [Getting Started](../01-Getting-Started/README.md)  |  Next: [Foundation](../03-Foundation/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Dependency Graph**

# Dependency Graph

## Contents

- [Dependency Graph](#dependency-graph-1)
- [Dependency Rules](#dependency-rules)
- [Foundation Contracts in Practice](#foundation-contracts-in-practice)

---

## Dependency Graph

The intended dependency graph is:

```
                 Foundation
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   Runtime           SQL      Mapping.Generators
      ▲               ▲               │
      │               │               │
      └───────────────┼───────────────┘
                      │
          Generated Runtime Components
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   GraphQL          gRPC          WebApi
```

Dependencies should always point toward more stable layers.

Circular project references should never be introduced.

---

## Dependency Rules

The following rules should always hold:

| Project | Allowed Dependencies |
|---------|-----------------------|
| Foundation | None |
| Runtime | Foundation |
| SQL | Foundation, Runtime |
| Mapping.Generators | Foundation, Roslyn |
| GraphQL | Foundation, Runtime |
| gRPC | Foundation, Runtime |
| WebApi | Foundation, Runtime |

Generated code depends only on Foundation contracts and is consumed through Dependency Injection.

---

## Foundation Contracts in Practice

## Foundation Contracts

The Runtime depends on interfaces defined by Foundation.

Typical contracts include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer

ISqlDialect

IGraphStrategy
```

Generated implementations satisfy these interfaces.

---

---

## Related Documentation

- [Layers](Layers.md)
- [Foundation → Contracts](../03-Foundation/Contracts.md)
- [Dependency Injection](../07-Dependency-Injection/README.md)

---

← Previous: [Request Pipeline](Request-Pipeline.md)  |  Next: [Foundation](../03-Foundation/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Layers**

# Layers

> **Note on today's physical layout.** The target layout below (`CoffeeBeanery.Foundation`,
> `CoffeeBeanery.Runtime`, `CoffeeBeanery.Sql`, etc. as separate projects) is the direction
> the solution is organized toward. Today, Foundation, Runtime, SQL, and GraphQL concerns
> live as folders inside the single `src/CoffeeBeanery` project (`GraphQL/Core/Runtime`,
> `GraphQL/Core/Sql`, `GraphQL/Core/Mapping`, `GraphQL/Core/GraphQL`), and the mapping
> generator lives in its own project
> (`CoffeeBeanery.GraphQL.Core.Mapping.Generators` in the sample solution).
> The dependency *rules* below already hold; the project *split* is incremental. See
> [Vision → Roadmap by phase](Vision.md#roadmap-by-phase).

## Contents

- [Design Goals](#design-goals)
- [Solution Layout](#solution-layout)
- [Foundation](#foundation)
- [Runtime](#runtime)
- [SQL](#sql)
- [Mapping Generator](#mapping-generator)
- [Emitters](#emitters)
- [Generated Output](#generated-output)
- [GraphQL](#graphql)
- [gRPC (future phase — not implemented today)](#grpc-future-phase--not-implemented-today)
- [Web API (future phase — not implemented today)](#web-api-future-phase--not-implemented-today)
- [Test Projects](#test-projects)

---

> This document defines the recommended repository layout for the CoffeeBeanery framework. The structure is designed to enforce architectural boundaries, support independent evolution of components, and simplify long-term maintenance.

---

## Design Goals

The repository is organized around the following principles:

- Clear dependency direction
- Single responsibility per project
- Transport independence
- Compile-time generation
- Native AOT compatibility
- Minimal project coupling

Every project should have one primary purpose.

---

## Solution Layout

```
CoffeeBeanery.sln

src/

    CoffeeBeanery.Foundation/

    CoffeeBeanery.Runtime/

    CoffeeBeanery.Sql/

    CoffeeBeanery.Mapping.Generators/

    CoffeeBeanery.GraphQL/

    CoffeeBeanery.Grpc/

    CoffeeBeanery.WebApi/

tests/

    CoffeeBeanery.Foundation.Tests/

    CoffeeBeanery.Runtime.Tests/

    CoffeeBeanery.Sql.Tests/

    CoffeeBeanery.Mapping.Generators.Tests/

    CoffeeBeanery.GraphQL.Tests/

    CoffeeBeanery.Grpc.Tests/

    CoffeeBeanery.WebApi.Tests/
```

Each project should be independently buildable and testable.

---

## Foundation

```
CoffeeBeanery.Foundation

Metadata/

Planning/

Interfaces/

Ids/

Primitives/

Exceptions/

Extensions/
```

Contains:

- Contracts
- Metadata
- Planning models
- Identifiers
- Primitive value objects

Never contains:

- Roslyn
- SQL
- Runtime
- GraphQL
- Generated code

Foundation is the most stable project in the solution.

---

## Runtime

```
CoffeeBeanery.Runtime

Planner/

Execution/

Mutation/

Query/

Materialization/

Services/

Interceptors/

DependencyInjection/
```

Contains:

- Query execution
- Mutation execution
- Runtime services
- Execution context
- Materialization coordination

Depends only on:

```
Foundation
```

---

## SQL

```
CoffeeBeanery.Sql

PostgreSql/

Builders/

Visitors/

Dialects/

Readers/

Writers/
```

Contains:

- SQL writers
- SQL readers
- SQL dialects
- SQL builders
- SQL visitors

Depends on:

```
Foundation

Runtime
```

---

## Mapping Generator

```
CoffeeBeanery.Mapping.Generators

Parser/

Validation/

Model/

Passes/

Emit/

Utilities/
```

Contains:

- Incremental generator
- Mapping parser
- Validation
- Identifier allocation
- Code emitters

Depends on:

```
Foundation

Roslyn
```

Never depends on Runtime.

---

## Emitters

Recommended emitter organization:

```
Emit/

IdEmitter.cs

MetadataEmitter.cs

PlannerEmitter.cs

MaterializerEmitter.cs

DematerializerEmitter.cs

InterceptorEmitter.cs

DependencyInjectionEmitter.cs

RuntimeRegistryEmitter.cs
```

Each emitter generates one category of source code.

---

## Generated Output

Generated files should resemble:

```
Generated/

GeneratedEntityIds.cs

GeneratedMetadataProvider.cs

GeneratedPlannerRegistry.cs

GeneratedMaterializers.cs

GeneratedDematerializers.cs

GeneratedInterceptors.cs

GeneratedServiceCollectionExtensions.cs
```

Generated code should contain registrations and precomputed data, not business logic.

---

## GraphQL

```
CoffeeBeanery.GraphQL

Schema/

Types/

Resolvers/

Middleware/

DependencyInjection/
```

Contains only GraphQL-specific concerns.

Depends on:

```
Foundation

Runtime
```

---

## gRPC *(future phase — not implemented today)*

```
CoffeeBeanery.Grpc

Services/

Protobuf/

Mapping/

DependencyInjection/
```

Contains only gRPC-specific integration.

Depends on:

```
Foundation

Runtime
```

---

## Web API *(future phase — not implemented today)*

```
CoffeeBeanery.WebApi

Controllers/

MinimalApis/

Filters/

DependencyInjection/
```

Contains ASP.NET Core transport logic only.

Depends on:

```
Foundation

Runtime
```

---

## Test Projects

Every production project should have a corresponding test project.

Recommended layout:

```
tests/

CoffeeBeanery.Foundation.Tests

CoffeeBeanery.Runtime.Tests

CoffeeBeanery.Sql.Tests

CoffeeBeanery.Mapping.Generators.Tests

CoffeeBeanery.GraphQL.Tests

CoffeeBeanery.Grpc.Tests

CoffeeBeanery.WebApi.Tests
```

Tests should mirror the production structure where practical.

---

## Dependency Graph

The intended dependency graph is:

```
                 Foundation
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   Runtime           SQL      Mapping.Generators
      ▲               ▲               │
      │               │               │
      └───────────────┼───────────────┘
                      │
          Generated Runtime Components
                      ▲
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   GraphQL          gRPC          WebApi
```

Dependencies should always point toward more stable layers.

Circular project references should never be introduced.

---

## Dependency Rules

The following rules should always hold:

| Project | Allowed Dependencies |
|---------|-----------------------|
| Foundation | None |
| Runtime | Foundation |
| SQL | Foundation, Runtime |
| Mapping.Generators | Foundation, Roslyn |
| GraphQL | Foundation, Runtime |
| gRPC | Foundation, Runtime |
| WebApi | Foundation, Runtime |

Generated code depends only on Foundation contracts and is consumed through Dependency Injection.

---

## Long-Term Evolution

This layout enables future additions without disturbing existing layers.

Potential new projects include:

```
CoffeeBeanery.Mongo

CoffeeBeanery.Cosmos

CoffeeBeanery.Redis

CoffeeBeanery.Elasticsearch

CoffeeBeanery.Blazor

CoffeeBeanery.OpenApi

CoffeeBeanery.Cli
```

Each new project integrates through Foundation contracts rather than modifying Runtime.

---

## Summary

The repository structure reinforces CoffeeBeanery's architectural principles:

- Stable Foundation contracts
- Transport-agnostic Runtime
- Database-specific SQL layer
- Compile-time source generation
- Thin transport adapters
- Dependency inversion
- Clear project boundaries
- Long-term maintainability

By organizing the solution around responsibilities rather than technologies, CoffeeBeanery remains modular, extensible, and adaptable as the framework grows.

---

## Related Documentation

- [Vision](Vision.md)
- [Dependency Graph](Dependency-Graph.md)
- [Foundation](../03-Foundation/README.md)
- [Persistence](../08-Persistence/README.md)

---

← Previous: [Principles](Principles.md)  |  Next: [Request Pipeline](Request-Pipeline.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Principles**

# Principles

## Contents

- [The Five Core Principles](#the-five-core-principles)
- [Extended Engineering Principles](#extended-engineering-principles)

---

## The Five Core Principles

Every other principle in this document is a refinement of these five. If a design decision
can't be justified by at least one of them, it doesn't belong in Coffee Beanery.

**1. Business First**
The domain is the source of truth. Infrastructure exists to serve it.

**2. Compile-Time by Default**
Discover as much as possible during compilation. Avoid runtime reflection and dynamic
behavior whenever practical.

**3. Deterministic Execution**
Every request should execute through a known, generated execution plan. Predictability is
more valuable than hidden magic.

**4. Provider-Based Architecture**
Execution is delegated to providers. Today that may be PostgreSQL. Tomorrow it may be
SQL Server, Kafka, Temporal, Redis, or something else. The planner doesn't change.

**5. Transport Agnostic**
GraphQL is not special. Neither is REST. Neither is gRPC. They are simply ways of entering
the execution engine.

## Extended Engineering Principles

These are the day-to-day engineering principles that fall out of the five core principles
above — stable guidance for anyone implementing a new provider, transport, or generator
stage.

> This document captures the fundamental engineering principles that guide every architectural and implementation decision within CoffeeBeanery. These principles are intentionally long-lived and should remain stable even as individual implementations evolve.

---

### Introduction

CoffeeBeanery is designed around a simple idea:

> **Move complexity to compile time so runtime can remain simple, deterministic, and fast.**

Every architectural decision should reinforce this objective.

---

### 1. Compile-Time First

Anything that can be computed during compilation should never be computed during execution.

Examples include:

- Metadata discovery
- Relationship resolution
- Identifier allocation
- Planner generation
- Materializer generation
- Dependency analysis

The Runtime should execute prepared artifacts rather than discover information dynamically.

---

### 2. Runtime Simplicity

Runtime exists to execute.

It should never perform:

- Reflection
- Metadata discovery
- Source analysis
- Dynamic code generation
- Attribute parsing

Execution should always operate on immutable, precomputed inputs.

---

### 3. Single Responsibility

Every architectural layer owns one responsibility.

| Layer | Responsibility |
|---------|----------------|
| Foundation | Contracts |
| Runtime | Execution |
| SQL | SQL serialization |
| Generator | Compile-time analysis |
| GraphQL | Transport |
| Generated Code | Precomputed data |

Responsibilities should not overlap.

---

### 4. Dependency Inversion

High-level components should depend on abstractions rather than generated implementations.

Instead of:

```
Runtime

↓

GeneratedMetadata
```

Prefer:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

Generated code becomes a replaceable implementation rather than an architectural dependency.

---

### 5. Immutable Metadata

Metadata represents facts about the application.

Facts should not change while the application is running.

Metadata objects should therefore be:

- Immutable
- Thread-safe
- Singleton
- Shared

Examples include:

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata

---

### 6. Immutable Execution Plans

Planning determines execution.

Execution should not modify planning decisions.

QueryPlan and MutationPlan should therefore be immutable representations of work to perform.

---

### 7. Explicit Architecture

Dependencies should always be visible.

Hidden dependencies, service locators, and implicit behavior should be avoided.

Architecture should be understandable by reading project references.

---

### 8. Transport Independence

GraphQL is one transport—not the framework.

The same Runtime should execute requests originating from:

- GraphQL
- gRPC
- REST
- CLI
- Background services

Execution semantics remain identical regardless of transport.

---

### 9. Storage Independence

Planning should remain independent of storage engines.

Only SQL serialization changes between providers.

Potential providers include:

- PostgreSQL
- SQL Server
- MySQL
- SQLite
- CockroachDB

The planner should not require modification.

---

### 10. Deterministic Generation

Running the Generator twice on identical source code should produce identical generated output.

Deterministic generation simplifies:

- Debugging
- Snapshot testing
- Source control
- Build reproducibility

---

### 11. Native AOT Compatibility

Native AOT is not a separate feature.

It is a consequence of good architecture.

Avoid:

- Reflection
- Runtime IL generation
- Dynamic proxies
- Expression compilation

Prefer generated implementations and static dispatch.

---

### 12. Performance Through Architecture

Performance should result from architectural choices rather than isolated optimizations.

Examples include:

- Compile-time generation
- Immutable metadata
- Array indexing
- Generated materializers
- Precomputed dependency graphs

Architecture should eliminate work rather than optimize unnecessary work.

---

### 13. Composition Over Inheritance

Framework behavior should be composed through interfaces.

Prefer:

```csharp
IMetadataProvider

ISqlDialect

IGraphStrategy
```

Avoid deep inheritance hierarchies.

Composition improves flexibility and testing.

---

### 14. Predictability

Execution should be deterministic.

Given the same:

- Metadata
- QueryPlan
- MutationPlan
- Database state

the framework should produce identical results.

Predictability simplifies debugging and testing.

---

### 15. Testability

Every major component should be testable in isolation.

Foundation should not require Runtime.

Runtime should not require SQL.

SQL should not require GraphQL.

Generator output should be snapshot tested.

Architecture should naturally encourage testing.

---

### 16. Readability Over Cleverness

Code is read more often than it is written.

Prefer explicit implementations over clever abstractions.

Generated code should be understandable.

Runtime should be easy to debug.

Simple code generally performs well enough and is easier to maintain.

---

### 17. Stable Contracts

Foundation represents the public architectural vocabulary.

Changes to Foundation should be deliberate and infrequent.

Stable contracts reduce churn throughout the framework.

---

### 18. Extensibility Through Interfaces

Extension points should be explicit.

Applications should customize behavior through interfaces rather than modifying Runtime.

Examples include:

- IMetadataProvider
- ISqlDialect
- IEntityMaterializer
- IPlannerRegistry
- IGraphStrategy

---

### 19. Layer Isolation

Every layer should know only what it needs.

```
Foundation

↑

Runtime

↑

Transport
```

No layer should bypass another through direct implementation knowledge.

---

### 20. Long-Term Maintainability

CoffeeBeanery is intended to evolve over many years.

Short-term convenience should never compromise long-term architectural consistency.

When evaluating new features, prioritize:

- Simplicity
- Stability
- Explicitness
- Testability
- Determinism

over minimal implementation effort.

---

### Summary

These principles define the architectural identity of CoffeeBeanery.

They guide every decision—from project organization and source generation to SQL serialization and runtime execution.

When multiple implementation options exist, the preferred choice is the one that best preserves:

- Compile-time generation
- Immutable metadata
- Dependency inversion
- Deterministic execution
- Transport independence
- Clear architectural boundaries

By consistently applying these principles, CoffeeBeanery remains performant, maintainable, extensible, and adaptable as the framework continues to grow.

---

## Related Documentation

- [Vision](Vision.md)
- [Layers](Layers.md)
- [Foundation → Extensibility](../03-Foundation/Extensibility.md)

---

← Previous: [Vision](Vision.md)  |  Next: [Layers](Layers.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Request Pipeline**

# Request Pipeline

## Contents

- [Overview](#overview)
- [Phase 1 — Compilation](#phase-1--compilation)
- [Runtime Begins](#runtime-begins)
- [Mutation Flow](#mutation-flow)
- [Responsibility Matrix](#responsibility-matrix)
- [Architectural Benefits](#architectural-benefits)

---

> This document follows a request from the moment an application calls CoffeeBeanery until the final object is returned. It explains which project is responsible for each stage and how compile-time generation and runtime execution interact.

---

## Overview

CoffeeBeanery is divided into two major phases:

```
Compile Time

↓

Generated Runtime Components

↓

Runtime Execution
```

Compile-time builds knowledge.

Runtime consumes knowledge.

---

## Phase 1 — Compilation

Compilation begins with application models.

```
Application

↓

Entity Classes

↓

Attributes

↓

Relationships
```

The application contains only business models.

No runtime metadata exists yet.

---

## Phase 2 — Roslyn

The Incremental Generator receives the Roslyn compilation.

```
C# Source

↓

Roslyn

↓

Symbols
```

Roslyn exposes:

- Types
- Properties
- Attributes
- Generic information

---

## Phase 3 — Parsing

CoffeeBeanery parses Roslyn symbols.

```
Roslyn Symbols

↓

EntityNode

↓

ModelNode

↓

RelationshipNode
```

Roslyn APIs disappear after this stage.

The remaining pipeline uses CoffeeBeanery's internal model.

---

## Phase 4 — Validation

Validation verifies correctness.

Examples:

```
Duplicate Columns

↓

Error
```

```
Missing Key

↓

Error
```

```
Invalid Relationship

↓

Error
```

Compilation stops immediately when validation fails.

---

## Phase 5 — Relationship Resolution

Relationships become explicit.

```
Customer

↓

Orders

↓

OrderLines
```

becomes immutable metadata.

Runtime never analyzes relationships again.

---

## Phase 6 — Identifier Allocation

Stable identifiers are assigned.

```
Customer

↓

EntityId = 0
```

```
Order

↓

EntityId = 1
```

These identifiers become array indexes throughout Runtime.

---

## Phase 7 — Metadata Construction

Metadata objects are built.

```
EntityNode

↓

EntityMetadata
```

```
RelationshipNode

↓

JoinMetadata
```

Metadata is immutable.

---

## Phase 8 — Source Generation

The Generator emits code.

Examples:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers

GeneratedEntityIds
```

Compilation finishes.

---

## Runtime Begins

Application startup registers generated components.

```
services

↓

AddGeneratedCoffeeBeanery()

↓

Dependency Injection
```

Runtime is now fully configured.

---

## Incoming Request

A request may originate from:

```
GraphQL

gRPC

REST

CLI

Background Service
```

Transport does not affect Runtime.

---

## Planner

The transport asks a planner to build a plan.

```
Request

↓

Planner

↓

QueryPlan
```

or

```
MutationPlan
```

Plans are immutable.

---

## Runtime

Runtime receives the plan.

```
QueryPlan

↓

Runtime
```

Runtime does not inspect CLR models.

Runtime does not discover metadata.

Runtime executes only.

---

## Metadata Lookup

Runtime requests metadata.

```
IMetadataProvider

↓

GeneratedMetadataProvider

↓

EntityMetadata
```

Metadata lookup is deterministic.

---

## SQL

Runtime delegates SQL generation.

```
QueryPlan

↓

SqlWriter

↓

SQL
```

SQL generation performs serialization only.

---

## Database

The SQL statement executes.

```
SQL

↓

Database

↓

Rows
```

The Runtime does not know database syntax.

---

## Materialization

Generated materializers convert rows into CLR objects.

```
Rows

↓

Generated Materializer

↓

Customer
```

No reflection occurs.

---

## Response

The transport receives the final object.

```
Runtime

↓

GraphQL

↓

JSON
```

or

```
Runtime

↓

gRPC

↓

Protobuf
```

Execution is complete.

---

## Mutation Flow

Mutation execution follows a similar pipeline.

```
Mutation

↓

Planner

↓

MutationPlan

↓

Dependency Graph

↓

SQL

↓

Generated Values

↓

Materialization

↓

Response
```

Dependency ordering has already been computed during planning.

---

## Responsibility Matrix

| Stage | Project |
|---------|----------|
| Model Discovery | Mapping.Generators |
| Validation | Mapping.Generators |
| Metadata Construction | Mapping.Generators |
| Planner Generation | Mapping.Generators |
| Metadata Contracts | Foundation |
| Runtime Execution | Runtime |
| SQL Serialization | Sql |
| Transport | GraphQL / gRPC / WebApi |

Each project owns exactly one concern.

---

## Architectural Benefits

This separation provides several advantages:

- Compile-time analysis
- Deterministic Runtime
- Native AOT compatibility
- Transport independence
- Database abstraction
- Clear dependency direction
- Excellent testability

Each layer remains focused on its own responsibility.

---

## Summary

CoffeeBeanery performs all expensive analysis during compilation, generating immutable runtime artifacts that are consumed by a lightweight execution engine.

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Source Generators → Pipeline Stages](../06-Source-Generators/Pipeline-Stages.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Layers](Layers.md)  |  Next: [Dependency Graph](Dependency-Graph.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Vision**

# Vision

## Contents

- [The bold statement](#the-bold-statement)
- [Philosophy](#philosophy)
- [What Coffee Beanery is](#what-coffee-beanery-is)
- [What Coffee Beanery doesn't want to own](#what-coffee-beanery-doesnt-want-to-own)
- [Mission](#mission)
- [Vision statement](#vision-statement)
- [Roadmap by phase](#roadmap-by-phase)

---

## The bold statement

> Coffee Beanery is a compile-time execution engine that transforms business models into
> deterministic execution plans, independent of transport, database, or infrastructure.
> **Everything else is an adapter.**

If you only remember one sentence from this document, remember that one. Every other page
in this documentation set is a consequence of it.

A shorter version of the same idea, if you need an elevator pitch:

> Coffee Beanery is the compile-time execution engine for .NET applications. It transforms
> business models into deterministic execution plans while allowing developers to choose the
> best transport, persistence, and infrastructure technologies without changing the business
> model.

And the front-page version:

> Coffee Beanery separates business intent from execution. Model your domain once. Generate
> deterministic execution plans. Integrate with the best tools — not replace them.

## Philosophy

> Software should describe what the business does, not how infrastructure works.

Applications should not be written *around* SQL, GraphQL, REST, Kafka, gRPC, databases, or
ORMs. Instead, they should describe the business. Coffee Beanery transforms those business
models into optimized execution plans that different providers can execute.

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

## What Coffee Beanery is

Coffee Beanery is **not** an ORM. It is **not** a GraphQL framework. It is **not** a workflow
engine. It is **not** a database abstraction layer. It is a **compile-time execution engine**.
Its one responsibility is to transform business intent into deterministic execution plans.
Everything else is delegated to a provider.

## What Coffee Beanery doesn't want to own

This is equally important, and it's a deliberate scope boundary, not an oversight. Coffee
Beanery intentionally does not compete with the best-in-class tools it sits between:

- **Hot Chocolate** remains the GraphQL framework.
- **Dapper** remains the lightweight SQL executor.
- **EF Core** remains the mapping model that supplies metadata.
- **Kafka** remains a messaging platform (future provider — not built yet).
- **Temporal** remains a workflow engine (future provider — not built yet).

Coffee Beanery sits *between* the transport and the infrastructure, generating the execution
plan that connects them.

## Mission

**Transform business models into deterministic execution plans.** That mission does not
change as new phases are added — it's the fixed point every future provider, transport, and
adapter is judged against.

The longer form: *empower developers to model their business once and execute it everywhere
through deterministic, compile-time generated execution plans.*

## Vision statement

> To become the execution engine of modern .NET applications by separating business intent
> from infrastructure concerns through compile-time planning and provider-based execution.

## Roadmap by phase

Framing the roadmap as phases of the *same* execution engine — rather than a list of
unrelated features — is deliberate. It keeps the vision ambitious while keeping the
implementation focused, and it tells contributors that every future feature is an extension
of this idea, not a change in direction.

**Phase 1 (current)**

- EF Core mapping as the metadata source.
- Hot Chocolate as the transport.
- PostgreSQL as the execution provider.
- Dapper as the SQL executor.

**Future phases**

- Additional execution providers (SQL Server, MySQL, etc.).
- Additional transports (REST, gRPC).
- Additional infrastructure providers (Kafka, Temporal, Redis, etc.).
- Optional higher-level modeling APIs, if they solve real user problems.

See [Reference → Roadmap](../13-Reference/Roadmap.md) for the detailed, phase-by-phase
breakdown, and [Layers](Layers.md) for how today's single-solution codebase maps onto this
target architecture.

---

## Related Documentation

- [Principles](Principles.md)
- [Layers](Layers.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)
- [Reference → FAQ](../13-Reference/FAQ.md)

---

← Previous: [Architecture](README.md)  |  Next: [Principles](Principles.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Foundation**

# Foundation

Foundation is the dependency-free contract layer everything else in Coffee Beanery builds
on. It defines *what* the system talks about — metadata shapes, planning primitives,
interfaces, identifiers — without knowing *how* any of it gets executed.

---

## Contents

- [Metadata](Metadata.md) — the compile-time knowledge Foundation defines
- [Contracts](Contracts.md) — interfaces, planning primitives, identifiers
- [Components](Components.md) — project structure and responsibilities
- [Extensibility](Extensibility.md) — the extension points Foundation exposes to providers and transports

---


## Philosophy

## Philosophy

Foundation answers one question:

> **What exists?**

It deliberately does **not** answer:

- How queries execute
- How SQL is generated
- How GraphQL works
- How metadata is discovered

Those responsibilities belong to higher layers.

---

---

## Related Documentation

- [Architecture → Layers](../02-Architecture/Layers.md)
- [Runtime](../04-Runtime/README.md)
- [Dependency Injection](../07-Dependency-Injection/README.md)

---

← Previous: [Architecture](../02-Architecture/README.md)  |  Next: [Runtime](../04-Runtime/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Components**

# Components

## Contents

- [Responsibilities](#responsibilities)
- [Project Structure](#project-structure)
- [Runtime Independence](#runtime-independence)
- [SQL Independence](#sql-independence)
- [Transport Independence](#transport-independence)
- [Native AOT](#native-aot)
- [Versioning](#versioning)

---

## Responsibilities

Foundation owns:

- Metadata definitions
- Runtime contracts
- Planning primitives
- Identifier types
- Shared value objects
- Core abstractions

Foundation never owns:

- Runtime execution
- SQL generation
- Roslyn
- GraphQL
- Source generation
- Database providers

---

## Project Structure

```
CoffeeBeanery.Foundation

Metadata/

Interfaces/

Planning/

Ids/

Primitives/

Utilities/
```

Each namespace contains immutable contracts shared across the framework.

---

## Runtime Independence

Foundation intentionally knows nothing about Runtime.

It should never reference:

- QueryExecutor
- MutationExecutor
- SQL writers
- Materializers
- GraphQL resolvers

This separation keeps contracts stable.

---

## SQL Independence

Foundation does not know SQL exists.

Metadata describes entities and relationships—not SQL syntax.

Identifier quoting, dialects, and serialization belong entirely to the SQL project.

---

## Transport Independence

Foundation has no knowledge of:

- GraphQL
- gRPC
- REST
- ASP.NET Core

Those projects simply consume Foundation contracts.

---

## Native AOT

Foundation naturally supports Native AOT because it contains:

- immutable objects
- interfaces
- value types
- compile-time metadata contracts

No reflection or runtime discovery should exist in Foundation.

---

## Versioning

Foundation should evolve slowly.

Breaking changes ripple throughout every dependent project.

Changes should prioritize:

- Backward compatibility
- Simplicity
- Stability
- Explicitness

Foundation is the most stable project in the solution.

---

---

## Related Documentation

- [Contracts](Contracts.md)
- [Architecture → Layers](../02-Architecture/Layers.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [Contracts](Contracts.md)  |  Next: [Extensibility](Extensibility.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Contracts**

# Contracts

## Contents

- [Interfaces](#interfaces)
- [Planning](#planning)
- [Identifiers](#identifiers)
- [Primitives](#primitives)

---

## Interfaces

Foundation defines the contracts implemented by generated code and consumed by Runtime.

Examples include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer
```

Runtime depends only upon these abstractions.

---

## Planning

Planning primitives describe work that Runtime will execute.

Examples include:

```
QueryPlan

MutationPlan

Projection

Selection

JoinPlan

GraphPlan
```

Planning primitives are immutable.

---

## Identifiers

Foundation defines strongly typed identifiers for generated artifacts.

Typical identifiers include:

```
EntityId

StorageEntityId

ModelId

FieldId

ColumnId

GraphId
```

Identifiers should be deterministic and generated at compile time.

---

## Primitives

Primitives represent reusable framework concepts.

Examples:

```
SortDirection

FilterOperation

JoinType

RelationshipKind

MutationOperation
```

Primitives should remain stable over time.

---

---

## Related Documentation

- [Metadata](Metadata.md)
- [Components](Components.md)
- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)

---

← Previous: [Metadata](Metadata.md)  |  Next: [Components](Components.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Extensibility**

# Extensibility

## Contents

- [Philosophy](#philosophy)
- [Architectural Principle](#architectural-principle)
- [Extension Categories](#extension-categories)
- [Best Practices](#best-practices)

---

> CoffeeBeanery is designed to be extended through well-defined contracts rather than inheritance or runtime discovery. This document describes the framework's extensibility model and identifies the supported extension points.

---

## Philosophy

CoffeeBeanery follows the **Open/Closed Principle**.

The framework should be:

- Open for extension
- Closed for modification

Applications should be able to customize behavior without changing the Runtime.

---

## Architectural Principle

Every extension point lives behind a Foundation interface.

```
Application

↓

Custom Implementation

↓

Foundation Interface

↓

Runtime
```

Runtime never depends upon application code directly.

---

## Extension Categories

CoffeeBeanery exposes extension points in several areas:

```
Metadata

Planning

SQL

Materialization

Dematerialization

Graph

Interceptors

Dependency Injection

Transports
```

Each category has a clearly defined responsibility.

---

## Metadata Providers

Metadata is supplied through `IMetadataProvider`.

```csharp
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort storageEntityId);

    ModelMetadata GetModel(ushort modelId);

    JoinMetadata? GetJoin(
        ushort leftStorageEntity,
        ushort rightStorageEntity);

    GraphMetadata? GetGraph(ushort graphId);
}
```

Most applications use the generated implementation.

Advanced scenarios may provide custom metadata sources.

---

## Planner Registry

The planner registry maps models to generated planners.

Example contract:

```csharp
public interface IPlannerRegistry
{
    QueryPlanner GetQueryPlanner(ushort modelId);

    MutationPlanner GetMutationPlanner(ushort modelId);
}
```

Generated registries are the default implementation.

---

## SQL Dialects

SQL generation is intentionally database-independent.

A dialect implementation owns provider-specific syntax.

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteReturning(...);

    void WriteConflict(...);
}
```

Potential implementations:

- PostgreSQL
- SQL Server
- MySQL
- SQLite
- Oracle

---

## SQL Writers

Applications may replace SQL writers.

Example:

```csharp
ISqlWriter
```

Possible customizations:

- Multi-tenant SQL
- Audit SQL
- Soft-delete behavior
- Vendor-specific optimizations

---

## Materializers

Materializers convert rows into CLR objects.

```csharp
IEntityMaterializer
```

Generated implementations should satisfy most scenarios.

Custom materializers may support:

- Immutable records
- Custom collections
- Domain object construction

---

## Dematerializers

Dematerializers convert CLR objects into mutation values.

```csharp
IEntityDematerializer
```

Custom implementations may support:

- Domain events
- Change tracking
- Alternate serialization

---

## Graph Strategy

Graph support is isolated behind a strategy interface.

Example:

```csharp
IGraphStrategy
```

Possible implementations:

- Apache AGE
- Neo4j bridge
- Custom graph database
- No-op implementation

This isolates graph behavior from Runtime.

---

## Interceptors

Interceptors provide lifecycle hooks.

Typical events include:

```
Before Planning

After Planning

Before SQL

After SQL

Before Execution

After Execution

Before Materialization

After Materialization
```

Interceptors should observe or augment behavior rather than replace core execution.

---

## Dependency Injection

Every generated component should be replaceable.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
                      GeneratedMetadataProvider>();
```

Applications may substitute:

```csharp
services.AddSingleton<IMetadataProvider,
                      CustomMetadataProvider>();
```

Runtime remains unchanged.

---

## Transport Extensions

Runtime is transport agnostic.

New transports can integrate by translating requests into immutable plans.

Potential transports include:

- GraphQL
- gRPC
- REST
- SignalR
- CLI
- Background workers

No Runtime changes should be required.

---

## Storage Providers

Although CoffeeBeanery currently targets PostgreSQL, the architecture supports additional storage engines.

Potential future providers:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

Each provider implements SQL abstractions while reusing the same planners and Runtime.

---

## Generator Extensions

The Mapping Generator can evolve through additional emitters.

Examples:

```
IdEmitter

MetadataEmitter

PlannerEmitter

MaterializerEmitter

InterceptorEmitter

DependencyInjectionEmitter
```

Future emitters can generate additional compile-time artifacts without affecting existing Runtime components.

---

## Best Practices

When extending CoffeeBeanery:

- Prefer interfaces over inheritance
- Preserve immutability
- Avoid reflection
- Respect project boundaries
- Keep generated code deterministic
- Register implementations through Dependency Injection

Extensions should integrate with the framework rather than bypass it.

---

## Summary

CoffeeBeanery is intentionally extensible through Foundation contracts.

By exposing clear interfaces for metadata, planning, SQL generation, materialization, graph strategies, and transports, the framework can evolve without compromising its core architecture of compile-time generation, immutable execution plans, and transport-independent Runtime.

---

## Related Documentation

- [Foundation → Contracts](Contracts.md)
- [Architecture → Principles](../02-Architecture/Principles.md)
- [Runtime → Events](../04-Runtime/Events.md)

---

← Previous: [Components](Components.md)  |  Next: [Runtime](../04-Runtime/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Foundation](README.md) → **Metadata**

# Metadata

## Contents

- [Metadata](#metadata-1)
- [Dependency Direction](#dependency-direction)
- [Immutability](#immutability)

---

## Metadata

Metadata represents immutable facts about the application's structure.

Typical metadata objects include:

```
EntityMetadata

ModelMetadata

ColumnMetadata

JoinMetadata

GraphMetadata

FieldMetadata

MutationColumn

ColumnReference
```

Metadata is generated during compilation and consumed during execution.

---

## Dependency Direction

Foundation sits at the bottom of the dependency graph.

```
Foundation
      ▲
      │
Runtime
      ▲
      │
SQL
      ▲
      │
Generated Code
      ▲
      │
GraphQL
gRPC
WebApi
```

Foundation references no other CoffeeBeanery project.

---

## Immutability

Every metadata object should be immutable.

Example:

```csharp
public sealed class EntityMetadata
{
    public ushort Id { get; }

    public string Name { get; }

    public ImmutableArray<ColumnMetadata> Columns { get; }
}
```

Immutable objects:

- are thread-safe
- simplify caching
- improve predictability
- eliminate synchronization

---

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Source Generators → Pipeline Stages](../06-Source-Generators/Pipeline-Stages.md)
- [Reference → Glossary](../13-Reference/Glossary.md)

---

← Previous: [Foundation](README.md)  |  Next: [Contracts](Contracts.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Runtime**

# Runtime

Runtime is where generated execution plans actually run. It never discovers metadata, parses
attributes, or generates SQL — that all happened at compile time, in
[Source Generators](../06-Source-Generators/README.md). Runtime's job is narrower and more
predictable: execute the plan it was handed.

---

## Contents

- [Execution](Execution.md) — the runtime pipeline, execution context, transactions, error handling
- [Queries](Queries.md) — how the query planner works
- [Mutations](Mutations.md) — how the mutation planner works
- [Events](Events.md) — extension points for observing execution

---

## Philosophy

## Philosophy

The Runtime has one responsibility:

> Execute immutable plans.

It should never discover information.

It should never infer behavior.

It should simply execute.

---

---

## Related Documentation

- [Foundation](../03-Foundation/README.md)
- [GraphQL](../05-GraphQL/README.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)

---

← Previous: [Foundation](../03-Foundation/README.md)  |  Next: [GraphQL](../05-GraphQL/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Events**

# Events

## Contents

- [Current State](#current-state)
- [Interceptors (extension point)](#interceptors-extension-point)
- [Diagnostics and Logging](#diagnostics-and-logging)
- [What's Not Built Yet](#whats-not-built-yet)

---

## Current State

Coffee Beanery does not yet have a first-class eventing or pub/sub system. What exists
today is narrower: **interceptors** as an extension point in the execution pipeline, and
structured logging/diagnostics hooks at the runtime level.

## Interceptors (extension point)

## Interceptors

Interceptors provide lifecycle hooks.

Typical events include:

```
Before Planning

After Planning

Before SQL

After SQL

Before Execution

After Execution

Before Materialization

After Materialization
```

Interceptors should observe or augment behavior rather than replace core execution.

---

## Diagnostics and Logging

Runtime emits structured diagnostics and logging around each pipeline stage — see [Execution](Execution.md#error-handling) for the error-handling model those hooks feed into.

## What's Not Built Yet

A first-class domain-event or outbox-style eventing model — the kind that would let a
mutation publish an event a Kafka or Temporal provider could consume — is tracked as part of
the [future phases](../02-Architecture/Vision.md#roadmap-by-phase) (additional infrastructure
providers), not as something available today. If you need this now, the supported extension
point is an interceptor, not an event bus.

---

## Related Documentation

- [Foundation → Extensibility](../03-Foundation/Extensibility.md)
- [Architecture → Vision](../02-Architecture/Vision.md)
- [Reference → Roadmap](../13-Reference/Roadmap.md)

---

← Previous: [Mutations](Mutations.md)  |  Next: [GraphQL](../05-GraphQL/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Execution**

# Execution

## Contents

- [Runtime Pipeline](#runtime-pipeline)
- [Execution Context](#execution-context)
- [Dependency Graph Execution](#dependency-graph-execution)
- [Materialization](#materialization)
- [Transactions](#transactions)
- [Error Handling](#error-handling)
- [Thread Safety](#thread-safety)

---

## Runtime Pipeline

Every request follows the same execution pipeline.

```
Immutable Plan

↓

Execution Context

↓

SQL Generation

↓

Database Execution

↓

Materialization

↓

Return Result
```

The Runtime coordinates each stage but delegates specialized work to other layers.

---

## Execution Context

The execution context carries request-scoped state.

Typical contents include:

- Database connection
- Transaction
- SQL parameters
- Cancellation token
- Execution options

Execution contexts should remain lightweight.

---

## Dependency Graph Execution

Mutations frequently depend on previously generated values.

Example:

```
Customer

↓

CustomerAddress

↓

CustomerOrder
```

The planner computes dependency ordering.

Runtime executes operations in dependency order.

No dependency analysis occurs during execution.

---

## Materialization

Runtime coordinates generated materializers.

```
DbDataReader

↓

Generated Materializer

↓

CLR Object
```

Runtime does not inspect CLR properties.

Generated code performs object construction.

---

## Transactions

Runtime coordinates transaction boundaries.

Typical workflow:

```
Begin Transaction

↓

Execute Plan

↓

Commit

↓

Return Result
```

Failures result in rollback.

Transaction policy remains transport-independent.

---

## Error Handling

Runtime reports execution failures through well-defined exception types.

Typical categories include:

- Validation
- Planning
- SQL
- Materialization
- Transaction
- Graph execution

Transport layers translate these exceptions into protocol-specific responses.

---

## Thread Safety

Runtime services should generally be stateless.

Immutable metadata and immutable execution plans naturally support concurrent execution.

Mutable state should remain confined to execution contexts.

---

---

## Related Documentation

- [Queries](Queries.md)
- [Mutations](Mutations.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Runtime](README.md)  |  Next: [Queries](Queries.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Mutations**

# Mutations

## Contents

- [Overview](#overview)

---

> Mutation Planning is responsible for transforming create, update, delete, upsert, connect, disconnect, and graph mutations into a deterministic execution graph. Unlike queries, mutations have ordering constraints, dependencies, transactional semantics, identity propagation, and conflict resolution. The Mutation Planner resolves these concerns before Runtime begins execution.

Runtime executes mutations.

The Mutation Planner understands mutations.

---

## Philosophy

Mutation planning follows one rule:

> **Determine every dependency before execution begins.**

Runtime should never discover ordering.

Runtime should never resolve dependencies.

Everything must already exist in the MutationPlan.

---

## High-Level Pipeline

```
Mutation Request

↓

Planner

↓

Metadata Resolution

↓

Dependency Analysis

↓

Graph Analysis

↓

Ordering

↓

MutationPlan
```

Planning finishes before execution starts.

---

## Why Mutation Planning Exists

Queries are read operations.

Mutations change state.

Changing state introduces additional complexity:

- Ordering
- Transactions
- Identity propagation
- Foreign keys
- Graph dependencies
- Conflict handling

The planner resolves all of these.

---

## Planner Responsibilities

The Mutation Planner is responsible for:

- Entity resolution
- Dependency analysis
- Identity propagation
- Lookup planning
- Upsert planning
- Graph mutation planning
- Conflict analysis
- Execution ordering
- Transaction boundaries

It never executes SQL.

---

## Runtime Responsibilities

Runtime receives a completed MutationPlan.

Runtime performs:

```
MutationPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime assumes the plan is valid.

---

## MutationPlan

A MutationPlan is immutable.

Example:

```
MutationPlan

├── Operations

├── Dependencies

├── Graph Operations

├── Lookups

├── Identity References

├── Execution Order

└── Transaction Scope
```

Everything required for execution is already known.

---

## Mutation Operations

Each mutation becomes an operation node.

Examples:

```
Insert

Update

Delete

Upsert

Lookup

Connect

Disconnect
```

Operations become vertices in an execution graph.

---

## Dependency Graph

Mutations naturally form a graph.

Example:

```
Customer

↓

Order

↓

OrderItem
```

OrderItem cannot execute before Order.

Order cannot execute before Customer.

The planner computes this graph.

---

## Dependency Resolution

Dependencies are explicit.

```
Row 0

↓

Row 4

↓

Row 8
```

Runtime never discovers dependency order.

---

## Identity Propagation

Generated identities become dependency references.

Example:

```
Customer.Id

↓

Order.CustomerId
```

Runtime copies values according to the plan.

It never searches for relationships.

---

## Reference Nodes

References are represented explicitly.

```
Reference

Source Row

↓

Target Row

↓

Target Column
```

References remain immutable.

---

## Lookup Planning

Lookups are planned separately.

Example:

```
Country

↓

Lookup

↓

CountryId
```

Runtime receives complete lookup instructions.

---

## Upsert Planning

Upserts require conflict analysis.

Planner determines:

- Conflict columns
- Update columns
- Insert columns
- Identity propagation

Runtime only serializes provider syntax.

---

## Graph Mutation Planning

Graph mutations extend dependency planning.

Example:

```
Customer

↓

Order

↓

OrderItem

↓

Product
```

Traversal order becomes execution order.

---

## Topological Ordering

Execution order is determined through topological sorting.

```
Dependencies

↓

Topological Sort

↓

Execution Sequence
```

Runtime executes sequentially.

---

## Cyclic Detection

Cycles must be detected during planning.

Example:

```
A

↓

B

↓

A
```

Planner reports diagnostics.

Runtime never receives cyclic plans.

---

## Conflict Resolution

Conflict behavior becomes metadata.

Examples:

```
Do Nothing

Update

Replace

Merge
```

Providers translate conflict semantics.

---

## Transaction Planning

Planner determines transactional scope.

```
Entire Mutation

↓

Single Transaction
```

Or

```
Nested Savepoints
```

Runtime coordinates transactions.

---

## Graph Merge Planning

Graph merges become explicit operations.

Example:

```
Customer

↓

CustomerCustomerEdge

↓

Customer
```

Graph operations are independent from SQL generation.

---

## Execution Arms

Independent mutation branches can execute separately.

Example:

```
Customer

↓

Order A

↓

OrderItem A
```

```
Customer

↓

Order B

↓

OrderItem B
```

Planner identifies execution arms.

Future runtimes may parallelize them safely.

---

## Mutation Metadata

Planner consumes:

```
EntityMetadata

MutationMetadata

JoinMetadata

LookupMetadata
```

Runtime never performs metadata analysis.

---

## Alias Allocation

Every mutation node receives a deterministic identifier.

Example:

```
m0

m1

m2

m3
```

Identifiers remain stable.

---

## Parameter Planning

Planner identifies parameter sources.

Examples:

- Literal values
- Generated IDs
- Lookup IDs
- Dependency references

Runtime simply binds values.

---

## Immutable Mutation Graph

Planner builds mutable graphs internally.

Runtime receives immutable graphs.

```
Builder

↓

MutationGraph

↓

MutationPlan
```

Mutation ends before execution begins.

---

## Validation

Planning validates:

- Missing keys
- Invalid references
- Cycles
- Duplicate identities
- Missing lookup values
- Unsupported mutations

Invalid plans are rejected.

---

## Determinism

The same mutation always produces:

- Same node IDs
- Same dependency graph
- Same execution order
- Same SQL structure

Determinism greatly improves testing.

---

## SQL Boundary

Mutation planning ends at:

```
MutationPlan
```

SQL generation begins afterwards.

Providers should never perform dependency analysis.

---

## Runtime Execution

Runtime executes according to the graph.

```
Node

↓

Dependencies Satisfied?

↓

Execute

↓

Propagate Identity

↓

Continue
```

Execution follows the plan exactly.

---

## Materialization

Materialization occurs after execution.

Generated materializers reconstruct:

- Updated entities
- Inserted entities
- Lookup results

No planning occurs.

---

## Testing

Mutation planning should be tested independently.

Recommended tests:

```
Dependency Tests

↓

Identity Tests

↓

Lookup Tests

↓

Topological Order Tests

↓

Snapshot Tests
```

Runtime assumes planner correctness.

---

## Native AOT

Mutation planning naturally supports Native AOT because it relies entirely on generated metadata and immutable models.

No runtime discovery or reflection is required.

---

## Future Evolution

Potential enhancements include:

- Cost-based scheduling
- Parallel execution planning
- Distributed execution
- Generated mutation planners
- Bulk mutation optimization
- Provider-aware planning

Each enhancement should preserve Runtime simplicity.

---

## Mutation Planner Checklist

Before adding mutation logic, ask:

- Is this dependency structural?
- Can it be resolved before execution?
- Is execution order deterministic?
- Is the graph immutable?
- Can Runtime avoid this work?
- Can it be independently tested?

If not, reconsider the design.

---

## Relationship to the Framework

The Mutation Planner forms the boundary between mutation intent and mutation execution.

```
Transport

↓

Mutation Planner

↓

MutationPlan

↓

Runtime

↓

SQL Provider

↓

Database
```

Runtime becomes an execution engine rather than a mutation analyzer.

---

## Summary

The Mutation Planning Architecture transforms mutation requests into immutable dependency graphs by resolving entity relationships, identity propagation, lookup operations, graph traversals, conflict semantics, and execution ordering before Runtime begins.

This design enables deterministic execution, simplified Runtime logic, provider-independent SQL generation, reliable transactional behavior, comprehensive testing, and full Native AOT compatibility while supporting increasingly sophisticated graph mutation scenarios.

---

## Related Documentation

- [Queries](Queries.md)
- [Execution](Execution.md)
- [GraphQL → Schema](../05-GraphQL/Schema.md)

---

← Previous: [Queries](Queries.md)  |  Next: [Events](Events.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Queries**

# Queries

## Contents

- [Philosophy](#philosophy)
- [High-Level Pipeline](#high-level-pipeline)
- [Why Planning Exists](#why-planning-exists)
- [Planner Responsibilities](#planner-responsibilities)
- [Runtime Responsibilities](#runtime-responsibilities)
- [QueryPlan](#queryplan)

---

## Philosophy

The planner exists for one purpose:

> **Convert intent into instructions.**

A request expresses *what* the client wants.

A QueryPlan describes *how* Runtime will obtain it.

---

## High-Level Pipeline

```
Transport Request

↓

Planner

↓

Metadata Resolution

↓

Relationship Resolution

↓

Projection Analysis

↓

Graph Planning

↓

QueryPlan
```

Planning completes before Runtime begins.

---

## Why Planning Exists

Without planning:

```
Request

↓

Runtime

↓

Analyze Metadata

↓

Build SQL

↓

Execute
```

With planning:

```
Request

↓

Planner

↓

QueryPlan

↓

Runtime

↓

Execute
```

Runtime becomes significantly simpler.

---

## Planner Responsibilities

The planner is responsible for:

- Entity resolution
- Relationship resolution
- Projection analysis
- Join planning
- Graph planning
- Filter normalization
- Ordering
- Pagination
- Aggregation planning
- Alias generation

The planner never executes SQL.

---

## Runtime Responsibilities

Runtime receives a completed plan.

Runtime performs:

```
QueryPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime never revisits planning decisions.

---

## QueryPlan

The QueryPlan is an immutable contract.

Example:

```text
QueryPlan

├── Root Entity
├── Projection
├── Filters
├── Ordering
├── Pagination
├── Graph
├── Joins
└── Result Shape
```

Everything Runtime needs already exists.

---

---

## Related Documentation

- [Execution](Execution.md)
- [Mutations](Mutations.md)
- [GraphQL → Pagination, Filtering & Sorting](../05-GraphQL/Pagination-Filtering-Sorting.md)

---

← Previous: [Execution](Execution.md)  |  Next: [Mutations](Mutations.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **GraphQL**

# GraphQL

## Contents

- [Schema](Schema.md) — how the schema is composed from generated node metadata
- [Resolvers](Resolvers.md) — how resolvers hand off to the runtime
- [Pagination, Filtering & Sorting](Pagination-Filtering-Sorting.md)

---

## Where GraphQL fits

GraphQL — specifically [Hot Chocolate](https://chillicream.com/docs/hotchocolate) — is
**Phase 1's transport**, not a permanent architectural commitment. See
[Architecture → Vision](../02-Architecture/Vision.md): the planner underneath doesn't know
or care that GraphQL is asking it for data. REST and gRPC are listed as future transports in
the same [roadmap](../13-Reference/Roadmap.md) for exactly this reason — they'd enter the
same execution engine through a different door.

That said, GraphQL is what's built and tested today, via:

```xml
<PackageReference Include="HotChocolate" Version="15.1.12" />
<PackageReference Include="HotChocolate.AspNetCore" Version="15.1.12" />
<PackageReference Include="HotChocolate.Data" Version="15.1.12" />
<PackageReference Include="HotChocolate.Types" Version="15.1.12" />
<PackageReference Include="HotChocolate.Types.Analyzers" Version="15.1.12" />
```

## How a request enters

1. Hot Chocolate parses and validates the incoming GraphQL document against the schema.
2. A resolver (see [Resolvers](Resolvers.md)) hands the requested field selection to Coffee
   Beanery's [runtime](../04-Runtime/README.md) rather than resolving it field-by-field itself.
3. The runtime's [query planner](../04-Runtime/Queries.md) turns that selection into a single
   execution plan against the compile-time metadata produced by the
   [mapping generator](../06-Source-Generators/Mapping-Generator.md).
4. One batched SQL statement executes via Dapper; results are mapped back and returned
   through Hot Chocolate's serialization.

## Related Documentation

- [Runtime](../04-Runtime/README.md)
- [Source Generators](../06-Source-Generators/README.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [Runtime](../04-Runtime/README.md)  |  Next: [Source Generators](../06-Source-Generators/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Pagination, Filtering & Sorting**

# Pagination, Filtering & Sorting

## Contents

- [Where it's implemented](#where-its-implemented)
- [Compile-time vs. runtime](#compile-time-vs-runtime)

---

## Where it's implemented

Paging, filtering, and ordering are implemented as first-class parts of the runtime, not as
Hot Chocolate middleware layered on top of an `IQueryable`:

- `GraphQL/Core/Runtime/Paging` — cursor-based pagination
- `GraphQL/Core/Runtime/Filtering` — filter construction
- `GraphQL/Core/Runtime/Ordering` — sort construction

These feed directly into the SQL query compiler (`SqlPagingCompiler`, `SqlWhereCompiler`,
`SqlOrderCompiler` in `GraphQL/Core/Runtime`) described in
[Persistence](../08-Persistence/README.md), which means a filtered, sorted, paginated query
is still resolved as a single generated SQL statement rather than an in-memory filter over a
fully materialized result set.

## Compile-time vs. runtime

The *shape* of what's filterable/sortable per field comes from compile-time metadata (see
[Foundation → Metadata](../03-Foundation/Metadata.md)); the specific filter/sort *values* in
a given request are naturally resolved at runtime, but without any reflection-based property
lookup — see [Performance → Benchmarks](../10-Performance/Benchmarks.md) for how the
mapping layer avoids that cost.

---

## Related Documentation

- [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md)
- [Runtime → Queries](../04-Runtime/Queries.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Resolvers](Resolvers.md)  |  Next: [Source Generators](../06-Source-Generators/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Resolvers**

# Resolvers

## Contents

- [The wrapper pattern](#the-wrapper-pattern)
- [Query handling](#query-handling)
- [Service layer](#service-layer)

---

## The wrapper pattern

Rather than one Hot Chocolate resolver method per field, Coffee Beanery routes GraphQL
requests through a small number of wrapper resolvers (`WrapperQueryResolver`,
`WrapperMutationResolver`) that parse the incoming field selection and hand it to the
runtime's [query planner](../04-Runtime/Queries.md) or
[mutation planner](../04-Runtime/Mutations.md) as a whole. This is what makes a single
GraphQL query resolve through one batched SQL statement instead of one query per field/edge.

## Query handling

The runtime's `Service` layer — `ProcessService`, `ProcessQuery`, `QueryHandler`,
`QueryResult` — sits between the GraphQL wrapper resolver and the SQL/Dapper execution
layer. It receives the parsed request, invokes the generated planner, and returns a
`QueryResult` the resolver serializes back to the client.

## Service layer

```
GraphQL Resolver (WrapperQueryResolver / WrapperMutationResolver)
        │
        ▼
ProcessService → ProcessQuery / QueryHandler
        │
        ▼
Runtime Query/Mutation Planner  (see Runtime → Execution)
        │
        ▼
SQL generation + Dapper execution  (see Persistence)
```

See [Runtime → Execution](../04-Runtime/Execution.md) for what happens once the planner has
control.

---

## Related Documentation

- [Schema](Schema.md)
- [Runtime → Execution](../04-Runtime/Execution.md)
- [Persistence](../08-Persistence/README.md)

---

← Previous: [Schema](Schema.md)  |  Next: [Pagination, Filtering & Sorting](Pagination-Filtering-Sorting.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Schema**

# Schema

## Contents

- [Where the schema comes from](#where-the-schema-comes-from)
- [Node metadata](#node-metadata)
- [Wrapper resolvers](#wrapper-resolvers)

---

## Where the schema comes from

The GraphQL schema is composed from the same EF Core mapping classes that drive the rest of
Coffee Beanery — there's no separate schema-first `.graphql` file to keep in sync. Each
mapping class (a `BaseModelMappingRegistration<T>`) contributes a node to the schema, built
from the `NodeMap` / `NodeTree` structures under `GraphQL/Core/GraphQL` and
`GraphQL/Core/Mapping` in the runtime project. See
[Foundation → Metadata](../03-Foundation/Metadata.md) for the underlying metadata shapes and
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md) for how
those mapping classes are compiled into that metadata.

## Node metadata

At the framework level, the schema is a graph of nodes and edges (`NodeTree`, `Edge`,
`GraphMap`, `LinkKey` — see `GraphQL/Core/Sql`), which is also what powers the graph-shaped
read path over PostgreSQL + Apache AGE described in
[Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md).

## Wrapper resolvers

The sample exposes queries and mutations through thin wrapper resolvers —
`WrapperQueryResolver` and `WrapperMutationResolver` in `Api/Api.Banking` — that delegate
into the runtime rather than hand-writing per-field resolution logic. See
[Resolvers](Resolvers.md) for how that handoff works.

---

## Related Documentation

- [Resolvers](Resolvers.md)
- [Foundation → Metadata](../03-Foundation/Metadata.md)
- [Getting Started → First Service](../01-Getting-Started/First-Service.md)

---

← Previous: [GraphQL](README.md)  |  Next: [Resolvers](Resolvers.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Source Generators**

# Source Generators

The Roslyn incremental source generator is what makes Coffee Beanery's "compile-time first"
principle real rather than aspirational. It reads your EF Core mapping classes and emits the
runtime's execution plan — no reflection, no runtime model discovery.

---

## Contents

- [Mapping Generator](Mapping-Generator.md) — the concrete generator shipped today, and what it requires of your mapping code
- [Diagnostics](Diagnostics.md) — the CBMAP diagnostic codes and known risk areas
- [Pipeline Stages](Pipeline-Stages.md) — the 12-stage compilation pipeline

---

## Philosophy

## Philosophy

The Generator has one responsibility:

> Analyze once during compilation so Runtime never has to analyze again.

Everything expensive should happen here.

Runtime should consume generated artifacts rather than discover application structure dynamically.

---

---

## Related Documentation

- [Foundation → Metadata](../03-Foundation/Metadata.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [GraphQL](../05-GraphQL/README.md)  |  Next: [Dependency Injection](../07-Dependency-Injection/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Source Generators](README.md) → **Diagnostics**

# Diagnostics

## Contents

- [Diagnostic Codes](#diagnostic-codes)
- [Known Risk Areas](#known-risk-areas)
- [Deterministic Output](#deterministic-output)
- [Testing](#testing)

---

## Diagnostic Codes

| Id | Mirrors (old runtime behavior) | Severity |
|---|---|---|
| CBMAP001 | `NodeBuilder` "WARNING: ... is type-incompatible with ..." | Warning |
| CBMAP002 | `NodeBuilder` "WARNING: ... has no matching property..." | Warning |
| CBMAP003 | `NodeBuilder.BuildEntityChildren` ambiguous-navigation exception | **Error** |
| CBMAP004 | (new) navigation-shaped property with no resolvable FK by convention | **Error** |
| CBMAP005 | (new) unsupported `BuildMap()` statement shape | **Error** |

CBMAP003 replaces a runtime `InvalidOperationException` for ambiguous navigations with a
build-time error — resolve it with a `ModelToEntity` alias entry, or the
`[EntityForeignKey]` escape hatch. See
[Mapping Generator → Ambiguous navigation handling](Mapping-Generator.md#ambiguous-navigation-handling)
for the full pattern.

## Known Risk Areas


> The Diagnostics subsystem is responsible for identifying architectural, modeling, and configuration issues during compilation rather than execution. Instead of allowing invalid applications to fail at runtime, CoffeeBeanery reports deterministic compiler diagnostics with actionable guidance, enabling developers to correct problems before the application is ever executed.

Diagnostics are part of the framework.

They are not an afterthought.

---

## Philosophy

Diagnostics follow one rule:

> **Every preventable runtime error should become a compile-time diagnostic.**

Compilation is the best opportunity to improve developer experience.

---

## Why Diagnostics?

Without diagnostics:

```
Compile

↓

Run

↓

Exception

↓

Debug
```

With diagnostics:

```
Compile

↓

Diagnostic

↓

Fix

↓

Run
```

Failures move left.

---

## High-Level Architecture

```
Source Code

↓

Parser

↓

Validation

↓

Diagnostics

↓

Generation
```

Invalid models never reach code generation.

---

## Responsibilities

The diagnostics subsystem is responsible for:

- Model validation
- Architecture validation
- Provider compatibility
- Metadata validation
- Graph validation
- Relationship validation
- Incremental diagnostics

Diagnostics never modify generated output.

---

## Diagnostic Lifecycle

Every diagnostic follows the same lifecycle.

```
Source

↓

Validation

↓

Diagnostic

↓

IDE

↓

Developer
```

Generation continues whenever possible.

---

## Diagnostic Categories

Diagnostics should be grouped by concern.

Examples:

```
Architecture

Metadata

Relationships

Planning

Providers

Generation

Performance
```

Each category should have a distinct identifier range.

---

## Identifier Convention

Diagnostic identifiers should remain stable.

Example:

```
CB1000

Architecture

CB2000

Metadata

CB3000

Relationships

CB4000

Planning

CB5000

Providers

CB9000

Internal Generator
```

Stable identifiers improve documentation and troubleshooting.

---

## Severity Levels

Diagnostics should clearly communicate severity.

```
Info

↓

Warning

↓

Error
```

Errors prevent generation.

Warnings allow generation.

---

## Error Philosophy

Errors indicate invalid applications.

Examples:

- Missing primary key
- Duplicate entity
- Circular dependency
- Invalid graph
- Unsupported mapping

Applications should not compile with structural errors.

---

## Warning Philosophy

Warnings indicate questionable designs.

Examples:

- Unused entity
- Redundant relationship
- Large projection
- Missing index recommendation
- Inefficient graph traversal

Warnings educate developers.

---

## Informational Diagnostics

Information diagnostics improve visibility.

Examples:

- Generated entity count
- Metadata statistics
- Incremental cache usage
- Optimization suggestions

Informational diagnostics should never block compilation.

---

## Validation Stages

Diagnostics may originate from multiple stages.

```
Syntax

↓

Semantic

↓

Model

↓

Metadata

↓

Planning
```

Each stage validates only its own responsibilities.

---

## Syntax Diagnostics

Examples include:

- Missing attributes
- Invalid declarations
- Unsupported modifiers

Syntax diagnostics occur before semantic analysis.

---

## Semantic Diagnostics

Examples:

- Unknown types
- Accessibility issues
- Generic misuse
- Invalid inheritance

Semantic analysis resolves compiler symbols.

---

## Model Diagnostics

Model validation includes:

- Duplicate entities
- Missing identifiers
- Invalid relationships
- Unsupported property types

Internal models should always be valid after this stage.

---

## Metadata Diagnostics

Metadata validation includes:

- Duplicate IDs
- Missing columns
- Invalid joins
- Graph inconsistencies

Runtime assumes metadata correctness.

---

## Planning Diagnostics

Planning validation includes:

- Cycles
- Invalid projections
- Ambiguous joins
- Unsupported filters

Invalid plans should never be generated.

---

## Provider Diagnostics

Providers may report compatibility issues.

Examples:

```
JSON not supported

Recursive CTE unavailable

Unsupported UPSERT strategy
```

Provider diagnostics should remain compile-time whenever possible.

---

## Analyzer Architecture

Analyzers should remain independent from generation.

Recommended structure:

```
Syntax Analyzer

Semantic Analyzer

Architecture Analyzer

Performance Analyzer

Provider Analyzer
```

Each analyzer owns one responsibility.

---

## Code Fixes

Many diagnostics should provide automatic fixes.

Examples:

```
Missing Attribute

↓

Add Attribute
```

```
Duplicate Identifier

↓

Generate New Identifier
```

Code fixes significantly improve developer experience.

---

## Diagnostic Messages

Messages should answer three questions:

1. What is wrong?
2. Why is it wrong?
3. How do I fix it?

Avoid vague diagnostics.

---

## Example Diagnostic

```
CB2004

Duplicate entity identifier.

The entity 'Customer' shares an identifier with
'Supplier'.

Assign unique identifiers or allow automatic
allocation.
```

The fix should be obvious.

---

## Diagnostic Location

Diagnostics should appear at the most relevant location.

Prefer:

```
Entity Declaration
```

Instead of:

```
Generated Code
```

Developers should never debug generated files.

---

## Incremental Diagnostics

Incremental generators should invalidate only affected diagnostics.

Changing:

```
Customer.cs
```

should not recompute diagnostics for unrelated entities.

---

## Performance Diagnostics

Future analyzers may detect:

- N+1 patterns
- Large projections
- Excessive joins
- Redundant graph traversals

Performance guidance belongs in the IDE.

---

## Architecture Diagnostics

Architectural analyzers may validate:

- Dependency direction
- Layer violations
- Provider boundaries
- Runtime dependencies

This helps preserve long-term architecture.

---

## Snapshot Testing

Diagnostics should be snapshot tested.

```
Input

↓

Diagnostics

↓

Snapshot
```

Changes become immediately visible during review.

---

## Documentation

Every diagnostic should have documentation.

Example:

```
CB3007

Relationship Cycle

Description

Example

Resolution

Related Diagnostics
```

Documentation should remain versioned.

---

## IDE Experience

Diagnostics should integrate naturally with:

- Visual Studio
- Rider
- VS Code

Developers should receive feedback while typing.

---

## Thread Safety

Analyzers should remain stateless.

All state should remain local to analysis.

Shared mutable state should be avoided.

---

## Native AOT

Diagnostics exist only during compilation.

They contribute nothing to runtime size or execution cost.

---

## Future Evolution

Potential future analyzers include:

- Security analyzer
- Authorization analyzer
- Migration analyzer
- SQL analyzer
- Query analyzer
- Graph optimization analyzer

Each analyzer should remain modular.

---

## Diagnostic Checklist

Before adding a new diagnostic, ask:

- Is this actionable?
- Can it be detected during compilation?
- Does it explain the fix?
- Is the identifier stable?
- Can it provide a code fix?
- Can it be independently tested?

If not, reconsider the design.

---

## Relationship to the Framework

Diagnostics surround the entire compile-time pipeline.

```
Source Code

↓

Analysis

↓

Diagnostics

↓

Generation

↓

Runtime
```

They improve the framework without increasing runtime complexity.

---

## Summary

The Diagnostics & Analyzer Architecture transforms structural, architectural, provider, and planning errors into clear compile-time diagnostics, allowing developers to correct issues before execution begins.

By combining incremental analyzers, deterministic validation, stable diagnostic identifiers, actionable messages, IDE integration, and optional code fixes, CoffeeBeanery delivers a modern developer experience while preserving a lightweight Runtime and strengthening the architectural integrity of the framework.

The generator's own README additionally flags these concrete risk areas for the first real
build against your mapping code:

- **`MappingClassParser`** only understands the exact statement shapes used in the sample's
  `ProductMapping.BuildMap()`. Any other shape (loops, conditionals, helper method calls)
  hits `CBMAP005` and needs the parser extended.
- **Enum dictionary parsing** is the most speculative part of the parser — it pattern-matches
  `{ Enum.Value.ToString(), (int)Enum.Value }` collection-initializer entries syntactically.
- **`EntityNavigationConvention`**'s principal-key convention (`"{RelatedType.Name}Key"`) is
  an assumption based on the sample mapping and may need adjusting for your schema.

## Deterministic Output

Generated output is deterministic — the same mapping input always produces the same
generated source, which matters for incremental build performance and for reviewable diffs
in generated code. See [Pipeline Stages](Pipeline-Stages.md#incremental-boundaries) for how
incremental generation scopes re-computation.

## Testing

See [Contributing → Testing](../12-Contributing/Testing.md) for the layered testing strategy
(parser tests, validation tests, identifier tests, snapshot tests) the generator is expected
to carry.

---

## Related Documentation

- [Mapping Generator](Mapping-Generator.md)
- [Pipeline Stages](Pipeline-Stages.md)
- [Contributing → Testing](../12-Contributing/Testing.md)

---

← Previous: [Mapping Generator](Mapping-Generator.md)  |  Next: [Pipeline Stages](Pipeline-Stages.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Source Generators](README.md) → **Mapping Generator**

# Mapping Generator

`CoffeeBeanery.GraphQL.Core.Mapping.Generators` is the concrete generator shipped in Phase 1.
It replaces `NodeBuilder<TContext>`'s five reflective passes — `InferModelChildren`,
`GenerateReflectedFieldMaps`, `ResolveFieldMapAliases`, `BuildTree`, `BuildModel` — with a
compile-time equivalent, so the mapping layer is Native AOT / trim safe with zero runtime
reflection. This page is the generator's own README, reproduced here as canonical
documentation rather than left buried in the sample project.

---

## Contents

- [Required changes to existing hand-written code](#required-changes-to-existing-hand-written-code)
- [Ambiguous navigation handling](#ambiguous-navigation-handling)
- [Diagnostics](#diagnostics)
- [Known risk areas to check on first build](#known-risk-areas-to-check-on-first-build)

---

Source generator that replaces `NodeBuilder<TContext>`'s five reflective passes
(`InferModelChildren`, `GenerateReflectedFieldMaps`, `ResolveFieldMapAliases`,
`BuildTree`, `BuildModel`) with a compile-time equivalent, so the mapping layer
is Native AOT / trim safe with zero runtime reflection.

**Status: not yet build-verified.** This sandbox has no .NET SDK, so the project
has been written and self-reviewed carefully but not compiled against your real
`CoffeeBeanery.GraphQL.Core.Mapping` / `.Sql` assemblies. Treat the first build
as the real validation step — see "Known risk areas" below for where I'd look
first if something doesn't compile.

## Required changes to existing hand-written code

1. **Mapping classes must be `partial`.**
   ```csharp
   public partial class ProductMapping : BaseModelMappingRegistration<Product>
   ```
   The generator emits the other half of the partial class containing the
   generated `Register()` override.

2. **`BaseModelMappingRegistration<T>.Register()` must be `virtual`.**
   The generated partial provides `public override void Register()`, which
   builds `ModelNodeTree` / `CoffeeBeanery.GraphQL.Core.Sql.EntityNodeTree` / `ModelNode` / `EntityNode`
   directly and calls `NodeRegistry.RegisterNode(...)` — it never calls
   `BuildMap()` or touches `NodeBuilder` at runtime. `BuildMap()` itself stays
   in your hand-written file purely as the *source of truth the generator
   parses at compile time* — it's read, never executed.

3. **`BaseModelMappingRegistration<T>` must expose the constructor's alias and
   model strings** as `protected string Alias` / `protected string ModelName`
   (rename in `NodeTreeEmitter.cs` if your actual property names differ —
   search for `this.Alias` / `this.ModelName`).

4. **Reference the generator as an analyzer**, not a normal assembly reference:
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\CoffeeBeanery.GraphQL.Core.Mapping.Generators\CoffeeBeanery.GraphQL.Core.Mapping.Generators.csproj"
                        OutputItemType="Analyzer"
                        ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

5. **Drop the `NodeBuilder<TContext>.BuildFromMappings()` call from startup.**
   Registration now happens per-instance via each mapping class's generated
   `Register()` override, wherever `new ProductMapping(...).Register()` is
   already called today (e.g. from `ProductMappingSet.Register`). Nothing
   about that call site needs to change — only what `Register()` *does*
   changes.

## Ambiguous navigation handling

Where `NodeBuilder.BuildEntityChildren` threw `InvalidOperationException` at
runtime for ambiguous navigations (e.g. an entity with two navigation
properties to the same related type), this generator instead emits a build
**error** (`CBMAP003`) pointing at the entity. Resolve it the same way as
before (a `ModelToEntity` alias entry matching the navigation name), or via
the new `[EntityForeignKey]` escape hatch for navigations not expressible via
the `{Nav}Key` / `{Related}Key` convention at all (e.g. only configured via
fluent EF `OnModelCreating`):

```csharp
[EntityForeignKey(typeof(Customer), foreignKeyProperty: "InnerCustomerKey",
    principalKeyProperty: "CustomerKey", navigationName: "InnerCustomer")]
[EntityForeignKey(typeof(Customer), foreignKeyProperty: "OuterCustomerKey",
    principalKeyProperty: "CustomerKey", navigationName: "OuterCustomer")]
public class CustomerCustomerRelationship { ... }
```

`EntityForeignKeyAttribute` is emitted automatically via
`RegisterPostInitializationOutput` — you don't need to add it by hand or
reference any extra package.

## Diagnostics

| Id      | Mirrors (old runtime behavior)                                    | Severity |
|---------|---------------------------------------------------------------------|----------|
| CBMAP001 | `NodeBuilder` "WARNING: ... is type-incompatible with ..."        | Warning  |
| CBMAP002 | `NodeBuilder` "WARNING: ... has no matching property..."          | Warning  |
| CBMAP003 | `NodeBuilder.BuildEntityChildren` ambiguous-navigation exception   | **Error**|
| CBMAP004 | (new) navigation-shaped property with no resolvable FK by convention | **Error**|
| CBMAP005 | (new) unsupported `BuildMap()` statement shape                    | **Error**|

## Known risk areas to check on first build

- **`MappingClassParser`**: only understands the exact statement shapes used
  in `ProductMapping.BuildMap()` (local `NodeMap` declaration with object
  initializer, `AddModelToEntity<,>(...)`, `FieldMaps.Add(new FieldMap{...})`,
  `ExcludedFieldMappings.Add(...)`, `UpsertKeys.Add(...)`, `return map;`). Any
  other mapping class with a different `BuildMap()` shape (loops, conditionals,
  helper method calls) will hit `CBMAP005` and needs the parser extended.
- **Enum dictionary parsing** (`EvaluateEnumDictionary`/`TryEvaluateEnumToString`/
  `TryEvaluateEnumCast`) is the most speculative part of the parser — it's
  pattern-matching `{ Enum.Value.ToString(), (int)Enum.Value }` collection
  initializer entries syntactically. If your real `FromEnum`/`ToEnum`
  dictionaries are built differently than `ProductMapping`'s example, this
  needs adjusting.
- **`EntityNavigationConvention`**'s principal-key convention
  (`"{RelatedType.Name}Key"`) is an assumption based on the sample mapping
  (`ContractKey`, `AccountKey`, etc.) — verify it holds across your full
  entity set, since this is the one pass with no equivalent in the original
  `NodeBuilder` (which got this from live EF metadata, not convention).
- **`required` members on netstandard2.0** — `Polyfills.cs` defines the
  `RequiredMemberAttribute`/`CompilerFeatureRequiredAttribute`/`IsExternalInit`
  shims needed for C# 11 `required`/`init` on a netstandard2.0 TFM. If your
  build pulls in another package defining these same internal types
  (unlikely, but possible with multiple generator projects in one solution),
  you'll get a duplicate-type error — delete one copy.
- **`BaseModelMappingRegistration<T>` field names** — I assumed `Alias` and
  `ModelName` based on the constructor signature shown
  (`ProductMapping(string alias, string model)`). Confirm/rename in
  `NodeTreeEmitter.EmitRegisterOverride`.

---

## Related Documentation

- [Diagnostics](Diagnostics.md)
- [Pipeline Stages](Pipeline-Stages.md)
- [Getting Started → First Service](../01-Getting-Started/First-Service.md#write-a-mapping-class)

---

← Previous: [Source Generators](README.md)  |  Next: [Diagnostics](Diagnostics.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Source Generators](README.md) → **Pipeline Stages**

# Pipeline Stages

## Contents

- [Overview](#overview)
- [Design Goals](#design-goals)
- [Stage 1 — Roslyn Discovery](#stage-1--roslyn-discovery)
- [Stage 2 — Semantic Analysis](#stage-2--semantic-analysis)
- [Stage 3 — Parsing](#stage-3--parsing)
- [Stage 4 — Validation](#stage-4--validation)
- [Stage 5 — Relationship Resolution](#stage-5--relationship-resolution)
- [Stage 6 — Identifier Allocation](#stage-6--identifier-allocation)
- [Stage 7 — Metadata Construction](#stage-7--metadata-construction)
- [Stage 8 — Planner Construction](#stage-8--planner-construction)
- [Stage 9 — Materialization Generation](#stage-9--materialization-generation)
- [Stage 10 — Dematerialization Generation](#stage-10--dematerialization-generation)
- [Stage 11 — Registry Generation](#stage-11--registry-generation)
- [Stage 12 — Dependency Injection](#stage-12--dependency-injection)
- [Incremental Boundaries](#incremental-boundaries)

---

> The CoffeeBeanery Mapping Generator is organized as a deterministic compilation pipeline. Each stage has a single responsibility, consumes immutable input, and produces immutable output for the next stage.

This document describes every stage of that pipeline.

---

## Overview

The generator follows a linear transformation model.

```
C# Source

↓

Roslyn

↓

Parser

↓

Semantic Model

↓

Validation

↓

Relationship Resolution

↓

Identifier Allocation

↓

Metadata Construction

↓

Planner Construction

↓

Code Emitters

↓

Generated Source
```

No stage should skip another.

---

## Design Goals

The generation pipeline is designed to be:

- Deterministic
- Incremental
- Testable
- Immutable
- Parallelizable
- Easy to debug

Each stage should be independently testable.

---

## Stage 1 — Roslyn Discovery

The Incremental Generator begins by discovering candidate syntax nodes.

Typical candidates include:

- Classes
- Records
- Interfaces
- Attributes

Only relevant syntax proceeds to semantic analysis.

---

## Stage 2 — Semantic Analysis

Syntax is transformed into Roslyn symbols.

Examples include:

```
INamedTypeSymbol

IPropertySymbol

IMethodSymbol
```

The remainder of the pipeline should operate on semantic information rather than syntax trees.

---

## Stage 3 — Parsing

The parser converts Roslyn symbols into CoffeeBeanery's internal model.

Example objects:

```
EntityNode

ModelNode

PropertyNode

GraphNode

RelationshipNode
```

This separates framework concepts from Roslyn APIs.

---

## Stage 4 — Validation

Validation ensures the internal model is consistent.

Typical checks include:

- Duplicate entities
- Duplicate columns
- Duplicate identifiers
- Missing keys
- Unsupported types
- Invalid graph definitions
- Circular references

Compilation should stop if validation fails.

---

## Stage 5 — Relationship Resolution

Relationships are resolved once during compilation.

Examples include:

```
One-to-One

One-to-Many

Many-to-Many

Graph Edge

Lookup

Ownership
```

Resolved relationships become immutable metadata.

---

## Stage 6 — Identifier Allocation

Stable identifiers are assigned.

Examples:

```
EntityId

StorageEntityId

ModelId

FieldId

ColumnId

GraphId

JoinId
```

Identifier allocation should remain deterministic across builds whenever possible.

---

## Stage 7 — Metadata Construction

The resolved model becomes immutable metadata.

Generated metadata includes:

```
EntityMetadata

ModelMetadata

ColumnMetadata

JoinMetadata

GraphMetadata
```

Metadata becomes the Runtime's source of truth.

---

## Stage 8 — Planner Construction

Planning metadata is generated.

Examples include:

- Query planners
- Mutation planners
- Projection descriptors
- Join descriptors
- Graph descriptors

Planning should require no runtime analysis.

---

## Stage 9 — Materialization Generation

Materializers are generated.

Example:

```
DbDataReader

↓

Generated Materializer

↓

CLR Object
```

No reflection is required during execution.

---

## Stage 10 — Dematerialization Generation

Dematerializers generate mutation values.

Example:

```
CLR Object

↓

Generated Dematerializer

↓

Mutation Values
```

Again, runtime property inspection is unnecessary.

---

## Stage 11 — Registry Generation

Generated registries connect Runtime to generated components.

Typical outputs:

```
GeneratedMetadataProvider

GeneratedPlannerRegistry

GeneratedMaterializers

GeneratedDematerializers
```

Registries implement Foundation interfaces.

---

## Stage 12 — Dependency Injection

The final stage generates registration code.

Example:

```csharp
services.AddGeneratedCoffeeBeanery();
```

Applications register generated services without knowing implementation details.

---

## Incremental Boundaries

Every stage should invalidate only when required.

Example:

```
Entity Change

↓

Entity Metadata

↓

Planner

↓

Materializer
```

Unrelated entities should not trigger full regeneration.

---

## Error Reporting

Errors should be reported as early as possible.

Prefer:

```
Parser Error

↓

Compilation Stops
```

rather than allowing invalid models to reach emitters.

Each diagnostic should include:

- Error code
- Description
- Source location
- Suggested fix

---

## Testing Strategy

Each stage should have dedicated tests.

Examples:

```
Parser Tests

Validation Tests

Relationship Tests

Identifier Tests

Metadata Tests

Emitter Tests

Snapshot Tests
```

Testing stages independently simplifies debugging.

---

## Performance

Generator performance should prioritize:

- Incremental execution
- Minimal allocations
- Cached intermediate models
- Limited Roslyn traversal
- Small invalidation scopes

Fast incremental builds improve the developer experience.

---

## Native AOT

The entire pipeline exists to eliminate runtime discovery.

Everything generated during compilation replaces runtime reflection and dynamic behavior, making Runtime naturally compatible with Native AOT.

---

## Summary

The CoffeeBeanery code generation pipeline transforms application models into immutable runtime artifacts through a series of deterministic compilation stages.

---

## Related Documentation

- [Mapping Generator](Mapping-Generator.md)
- [Diagnostics](Diagnostics.md)
- [Foundation → Metadata](../03-Foundation/Metadata.md)

---

← Previous: [Diagnostics](Diagnostics.md)  |  Next: [Dependency Injection](../07-Dependency-Injection/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Dependency Injection**

# Dependency Injection

## Contents

- [Registration](Registration.md) — the composition root and per-layer registration
- [Lifetimes](Lifetimes.md) — lifetime guidelines and testing

---

## Philosophy

## Philosophy

Dependency Injection answers one question:

> **How are framework components composed?**

It should never determine:

- Query behavior
- SQL generation
- Planning logic
- Metadata construction

Those responsibilities belong elsewhere.

---

## Architectural Role

## Architectural Role

Dependency Injection sits at the composition root.

```
Application

↓

Dependency Injection

↓

Runtime

↓

Foundation Contracts

↓

Generated Implementations
```

Runtime depends only upon abstractions.

Applications decide which implementations to register.

---

---

## Related Documentation

- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)
- [Getting Started → Configuration](../01-Getting-Started/Configuration.md)

---

← Previous: [Source Generators](../06-Source-Generators/README.md)  |  Next: [Persistence](../08-Persistence/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Dependency Injection](README.md) → **Lifetimes**

# Lifetimes

## Contents

- [Lifetime Guidelines](#lifetime-guidelines)
- [Replacing Implementations](#replacing-implementations)
- [Avoid Service Location](#avoid-service-location)
- [Testing](#testing)

---

## Lifetime Guidelines

Recommended service lifetimes:

| Component | Lifetime |
|-----------|----------|
| Metadata Provider | Singleton |
| Planner Registry | Singleton |
| SQL Dialect | Singleton |
| Graph Strategy | Singleton |
| Query Executor | Singleton |
| Mutation Executor | Singleton |
| Materializers | Singleton |
| Dematerializers | Singleton |

Execution state belongs in scoped execution contexts rather than service instances.

---

## Replacing Implementations

Applications may replace any generated implementation.

Example:

```csharp
services.Replace(

    ServiceDescriptor.Singleton<
        IMetadataProvider,
        CustomMetadataProvider>());
```

Runtime requires no modification.

---

## Testing

Dependency Injection makes testing straightforward.

Example:

```csharp
services.AddSingleton<IMetadataProvider,
    TestMetadataProvider>();
```

Unit tests can replace:

- Metadata
- Planner registry
- SQL dialect
- Graph strategy

without changing Runtime.

---

## Avoid Service Location

Runtime should receive dependencies through constructors.

Preferred:

```csharp
public QueryExecutor(
    IMetadataProvider metadata,
    ISqlWriter writer)
{
}
```

Avoid resolving services directly from `IServiceProvider`.

Constructor injection makes dependencies explicit and easier to test.

---

---

## Related Documentation

- [Registration](Registration.md)
- [Contributing → Testing](../12-Contributing/Testing.md)

---

← Previous: [Registration](Registration.md)  |  Next: [Persistence](../08-Persistence/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Dependency Injection](README.md) → **Registration**

# Registration

## Contents

- [Composition Root](#composition-root)
- [Foundation Contracts](#foundation-contracts)
- [Generated Registration](#generated-registration)
- [Runtime Registration](#runtime-registration)
- [SQL Registration](#sql-registration)
- [GraphQL Registration](#graphql-registration)

---

## Composition Root

Each transport owns its own composition root.

Examples:

```
CoffeeBeanery.GraphQL

CoffeeBeanery.WebApi

CoffeeBeanery.Grpc
```

Each project registers Runtime plus generated services.

---

## Foundation Contracts

The Runtime depends on interfaces defined by Foundation.

Typical contracts include:

```csharp
IMetadataProvider

IPlannerRegistry

IEntityMaterializer

IEntityDematerializer

ISqlDialect

IGraphStrategy
```

Generated implementations satisfy these interfaces.

---

## Generated Registration

The Generator should emit a registration extension.

Example:

```csharp
public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection
        AddGeneratedCoffeeBeanery(
            this IServiceCollection services)
    {
        services.AddSingleton<IMetadataProvider,
            GeneratedMetadataProvider>();

        services.AddSingleton<IPlannerRegistry,
            GeneratedPlannerRegistry>();

        return services;
    }
}
```

Generated code contains registrations—not application logic.

---

## Runtime Registration

Runtime exposes its own registration extension.

Example:

```csharp
public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection
        AddCoffeeBeaneryRuntime(
            this IServiceCollection services)
    {
        services.AddSingleton<IQueryExecutor,
            QueryExecutor>();

        services.AddSingleton<IMutationExecutor,
            MutationExecutor>();

        return services;
    }
}
```

Runtime never registers generated components.

---

## SQL Registration

SQL providers expose separate registration methods.

Example:

```csharp
services.AddPostgreSql();
```

Internally this registers:

- ISqlWriter
- ISqlReader
- ISqlDialect
- IGraphStrategy

Database providers remain modular.

---

## GraphQL Registration

GraphQL composes the complete framework.

Typical setup:

```csharp
services

    .AddCoffeeBeaneryRuntime()

    .AddGeneratedCoffeeBeanery()

    .AddPostgreSql()

    .AddCoffeeBeaneryGraphQL();
```

GraphQL becomes a thin adapter over Runtime.

---

---

## Related Documentation

- [Lifetimes](Lifetimes.md)
- [Getting Started → Configuration](../01-Getting-Started/Configuration.md)
- [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md)

---

← Previous: [Dependency Injection](README.md)  |  Next: [Lifetimes](Lifetimes.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Persistence**

# Persistence

Persistence is where a generated execution plan meets an actual database. Phase 1 ships one
execution provider — PostgreSQL, with Apache AGE for graph-shaped reads — but the SQL layer
is deliberately structured as a **provider**, not baked into the runtime, so
[future phases](../02-Architecture/Vision.md#roadmap-by-phase) can add SQL Server, MySQL, or
others without the planner changing. See [Architecture → Vision](../02-Architecture/Vision.md).

---

## Contents

- [PostgreSQL & AGE](PostgreSQL-AGE.md) — the Phase 1 execution provider and the graph read path
- [Dapper & EF Core](Dapper-EFCore.md) — how the two coexist (metadata vs. execution)
- [Caching](Caching.md) — the warmup pipeline and in-process caching

---

## Philosophy

## Philosophy

The SQL layer has one responsibility:

> Convert execution plans into SQL.

It should never:

- Discover metadata
- Analyze CLR models
- Parse GraphQL
- Resolve relationships
- Perform planning

Planning belongs to the Runtime and Generator.

---

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [Dependency Injection](../07-Dependency-Injection/README.md)  |  Next: [AI & LLM Readiness](../09-AI/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **Caching**

# Caching

## Contents

- [Startup warmup](#startup-warmup)
- [In-process caching](#in-process-caching)
- [What warmup produces](#what-warmup-produces)

---

## Startup warmup

Before the first request is served, `GraphWarmup.Init` runs a warmup pipeline:

1. **Mapping set discovery** — scans the assembly for every `IMappingSet` implementation and
   registers it against both the model-type and entity-type axes.
2. **Property cache population** — `MappingWarmup.WarmupMap` walks every `FieldMap` and stores
   resolved `PropertyInfo` objects in `NodeMap.ModelProperties` / `NodeMap.EntityProperties`,
   eliminating per-request `Type.GetProperty` calls.
3. **Delegate compilation** — `BulkMapper.Compile` builds `Expression`-based getter/setter
   delegates, compiled to IL via `Expression.Lambda.Compile()`, cached in a
   `ConcurrentDictionary` keyed by `TypeFullName.PropertyName`.
4. **NodeTree generation** — `NodeTreeIterator.GenerateTree` pre-builds the full traversal
   tree for every root mapping.

By the time the first request arrives, the mapping layer has no reflection work left.

## In-process caching

The runtime's `CacheHelper` (`Cache/CacheHelper.cs`) provides in-process caching backed by
`FasterKv.Cache.Core` — no external cache server is required to run the sample locally. This
is separate from [future infrastructure providers](../02-Architecture/Vision.md#roadmap-by-phase)
like Redis, which are roadmap items for distributed caching, not what's used today.

## What warmup produces

Three `ConcurrentDictionary` caches — `_propCache`, `_getterCache`, `_setterCache` — populated
once at startup and read on every request afterward. See
[Performance → Benchmarks](../10-Performance/Benchmarks.md) for the measured effect.

---

## Related Documentation

- [Dapper & EF Core](Dapper-EFCore.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)
- [Runtime → Execution](../04-Runtime/Execution.md)

---

← Previous: [Dapper & EF Core](Dapper-EFCore.md)  |  Next: [AI & LLM Readiness](../09-AI/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **Dapper & EF Core**

# Dapper & EF Core

## Contents

- [Two different jobs](#two-different-jobs)
- [EF Core: metadata source](#ef-core-metadata-source)
- [Dapper: execution](#dapper-execution)
- [Query & Mutation Generation](#query--mutation-generation)

---

## Two different jobs

It's easy to assume Coffee Beanery is "an EF Core + Dapper hybrid ORM." It's more precise to
say: **EF Core supplies metadata, Dapper executes.** They're not layered or composed at
runtime — EF Core's mapping classes are read by the
[mapping generator](../06-Source-Generators/Mapping-Generator.md) at compile time, and by
request time, EF Core isn't in the path at all.

## EF Core: metadata source

Mapping classes (`BaseModelMappingRegistration<T>`, `BuildMap()`) describe the relationship
between your domain model and your EF Core entity model. The generator parses that
description at compile time — see
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md#required-changes-to-existing-hand-written-code)
for the exact shape it expects.

## Dapper: execution

At request time, generated SQL executes through `Dapper.Contrib` and `Z.Dapper.Plus` (for
bulk upserts) — no `DbContext`, no EF Core change tracking, no reflection-based materialization.
Rows come back through `Mapper.MapByAlias`, using pre-compiled getter/setter delegates built
during [warmup](Caching.md). See
[Performance → Benchmarks](../10-Performance/Benchmarks.md#why-response-times-are-this-low)
for the concrete mechanism.

## Query & Mutation Generation

## Query Generation

Typical pipeline:

```
Projection

↓

FROM

↓

JOIN

↓

WHERE

↓

GROUP BY

↓

ORDER BY

↓

LIMIT

↓

OFFSET
```

Each clause should be generated independently.

---

## Mutation Generation

Mutation generation typically consists of:

```
INSERT

↓

ON CONFLICT

↓

DO UPDATE

↓

RETURNING
```

or

```
WITH

↓

Dependency CTEs

↓

INSERT

↓

RETURNING
```

Dependency ordering is supplied by the Runtime.

---

---

## Related Documentation

- [PostgreSQL & AGE](PostgreSQL-AGE.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [PostgreSQL & AGE](PostgreSQL-AGE.md)  |  Next: [Caching](Caching.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **PostgreSQL & AGE**

# PostgreSQL & AGE

## Contents

- [Why PostgreSQL is Phase 1](#why-postgresql-is-phase-1)
- [Apache AGE and the graph read path](#apache-age-and-the-graph-read-path)
- [Provider Architecture](#provider-architecture)
- [SQL Writers, Readers & Dialects](#sql-writers-readers--dialects)

---

## Why PostgreSQL is Phase 1

PostgreSQL is Coffee Beanery's first execution provider, wired through Npgsql
(`Npgsql.EntityFrameworkCore.PostgreSQL`). Nothing in the [runtime](../04-Runtime/README.md)
or [Foundation contracts](../03-Foundation/Contracts.md) assumes PostgreSQL specifically —
see [Provider Architecture](#provider-architecture) below — but it's the only provider that's
actually implemented and tested today.

## Apache AGE and the graph read path

The sample's `Database.Graph.Banking` project layers [Apache AGE](https://age.apache.org/)
(a graph extension for PostgreSQL) on top of the relational schema, exposed through
`AgeConnectionFactory`, `GraphMap`, `Edge`, and `LinkKey` in `GraphQL/Core/Sql`. This is what
lets the GraphQL schema's node/edge shape (see [GraphQL → Schema](../05-GraphQL/Schema.md))
map naturally onto graph traversal for relationship-heavy queries, without hand-written
recursive joins.

## Provider Architecture

> Providers are the abstraction layer between the CoffeeBeanery Runtime and a specific persistence technology. They translate execution plans into provider-specific operations while preserving the semantics established during planning. Providers understand databases, transports, and protocols—but they never understand the application's domain model.

Providers encapsulate infrastructure.

They do not own business logic.

---

## Philosophy

Providers follow one rule:

> **The Runtime understands execution. Providers understand infrastructure.**

Responsibilities should never overlap.

---

## Why Providers?

Without providers:

```
Runtime

↓

PostgreSQL

↓

Execution
```

Supporting another database requires modifying Runtime.

With providers:

```
Runtime

↓

IProvider

↓

PostgreSQL

SQL Server

SQLite

MySQL
```

Runtime never changes.

---

## High-Level Architecture

```
Execution Plan

↓

Runtime

↓

Provider

↓

Infrastructure

↓

Results
```

Providers isolate infrastructure concerns.

---

## Provider Responsibilities

Providers are responsible for:

- SQL serialization
- Connection management
- Command execution
- Parameter binding
- Transaction integration
- Result stre

## SQL Writers, Readers & Dialects

## SQL Writers

SQL writers serialize execution plans.

Typical responsibilities:

- SELECT
- INSERT
- UPDATE
- DELETE
- UPSERT
- RETURNING
- Common Table Expressions (CTEs)

Writers should remain declarative.

---

## SQL Readers

Readers convert raw database results into structures suitable for materialization.

Responsibilities include:

- DbDataReader helpers
- Typed value access
- Database-specific conversions

Readers should not construct application models.

---

## SQL Dialects

Database-specific syntax belongs to dialect implementations.

Example interface:

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);

    void WriteLimit(...);

    void WriteOffset(...);

    void WriteConflict(...);

    void WriteReturning(...);
}
```

Supported dialects may include:

- PostgreSQL
- SQL Server
- SQLite
- MySQL
- Oracle

---

---

## Related Documentation

- [Dapper & EF Core](Dapper-EFCore.md)
- [GraphQL → Schema](../05-GraphQL/Schema.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Persistence](README.md)  |  Next: [Dapper & EF Core](Dapper-EFCore.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **AI & LLM Readiness**

# AI & LLM Readiness

## Contents

- [Scope note](#scope-note)
- [LLM Readiness](LLM-Readiness.md)

---

## Scope note

This section is not about AI/ML features *inside* Coffee Beanery's runtime — there is no
embedding, inference, or LLM-orchestration code in the framework today (see the
[Roadmap](../13-Reference/Roadmap.md); it isn't planned for Phase 1 or the immediately
following phases either). It's about making the documentation itself consumable by AI
coding assistants and LLM-based tooling, alongside humans, via the `llms.txt` convention.

See [LLM Readiness](LLM-Readiness.md) for what that means in practice and how the generated
`llms.txt` / `llms-full.md` files at the repository root relate to this documentation set.

---

## Related Documentation

- [Documentation Home](../README.md)
- [Reference](../13-Reference/README.md)

---

← Previous: [Persistence](../08-Persistence/README.md)  |  Next: [Performance](../10-Performance/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [AI & LLM Readiness](README.md) → **LLM Readiness**

# LLM Readiness

## Contents

- [What llms.txt is](#what-llmstxt-is)
- [How Coffee Beanery uses it](#how-coffee-beanery-uses-it)
- [Keeping it accurate](#keeping-it-accurate)

---

## What llms.txt is

[`llms.txt`](https://llmstxt.org/) is a proposed convention — a plain-Markdown index at a
project's root that gives AI assistants and LLM-based tools a concise, curated map of a
project's documentation, instead of forcing them to crawl and guess at an entire site.
`llms-full.md` is the companion convention for a single, complete concatenation of the
underlying docs, for tools that ingest one file rather than following links.

## How Coffee Beanery uses it

- **`/llms.txt`** — a short, curated index pointing at each section of this documentation
  set, in the same order as [the docs hub](../README.md).
- **`/llms-full.md`** — the full content of this documentation set concatenated into one
  file, for tools that prefer a single ingest target.
- **`/AI.SEO.md`** — kept as an alias of `llms.txt`'s intent for tools that specifically look
  for an `AI.SEO.md` / `ai-seo.md` file at the repository root, so discovery doesn't depend
  on which convention a given tool implements.

All three are **generated from this documentation set**, not maintained by hand — see
[Keeping it accurate](#keeping-it-accurate).

## Keeping it accurate

Previously, `llms.txt`, `llms-full.md`, and `AI.SEO.md` had drifted into three
byte-for-byte identical copies of one draft document, unrelated to the actual `docs/`
structure. That's fixed as part of this restructuring — see the
[archive](../../docs/archive/README.md) for what the old copies looked like. Going forward,
regenerate all three whenever a section is added or renamed under `docs/`, so an LLM
reading `llms.txt` sees the same section list a human sees at [`docs/README.md`](../README.md).

---

## Related Documentation

- [Documentation Home](../README.md)
- [Reference](../13-Reference/README.md)

---

← Previous: [AI & LLM Readiness](README.md)  |  Next: [Performance](../10-Performance/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Performance**

# Performance

## Contents

- [Native AOT](Native-AOT.md) — the design that makes AOT compatibility possible
- [Benchmarks](Benchmarks.md) — measured results against the sample Banking domain

---

## Core Principles

## Core Principles

CoffeeBeanery follows six performance principles:

- Compile-time over runtime
- Immutable metadata
- Deterministic execution
- Zero reflection
- Allocation awareness
- Cache-friendly data structures

Every optimization should support one or more of these principles.

---

---

## Related Documentation

- [Runtime → Execution](../04-Runtime/Execution.md)
- [Persistence → Caching](../08-Persistence/Caching.md)
- [Source Generators](../06-Source-Generators/README.md)

---

← Previous: [AI & LLM Readiness](../09-AI/README.md)  |  Next: [Samples](../11-Samples/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Performance](README.md) → **Benchmarks**

# Benchmarks

## Contents

- [Overview](#overview)
- [Why Response Times Are This Low](#why-response-times-are-this-low)
- [Results](#results)

---

> **Conditions:** No application-level caching. PostgreSQL built-in query cache only.
> **Tool:** Apidog
> **Date:** June 2026

---

## Overview

Each test executes a full round-trip against a live PostgreSQL instance:

1. **Mutation (upsert)** — inserts or updates all entities across the full relationship graph using `INSERT ... ON CONFLICT DO UPDATE`
2. **Filtered query** — a single batched `SELECT` with nested `LEFT JOIN` / `JOIN` chains that resolves the entire object graph in one database call
3. **Entity-to-model mapping** — the raw Dapper rows are mapped back to domain models using pre-compiled expression delegates, with zero reflection cost at request time

The `Product` model in these tests spans **4 physical tables** (`Banking.CustomerBankingRelationship`, `Lending.Contract`, `Account.Account`, `Lending.Transaction`). A single GraphQL query touching one customer with one product generates:

- **10 upsert statements** (writes across all 4 tables + relationship resolution steps)
- **1 SELECT** joining 5 tables across 3 schemas with 4 levels of nesting — resolved in a single database round trip
- **0 reflection overhead** at mapping time — all property access goes through pre-compiled lambda delegates

---

## Why Response Times Are This Low

### Startup Warmup Pipeline

Before the first request is served, `GraphWarmup.Init` executes a full warmup pipeline:

1. **Mapping set discovery** — scans the assembly for all `IMappingSet` implementations and registers them against both enum axes (model type + entity type)
2. **Property cache population** — `MappingWarmup.WarmupMap` walks every `FieldMap` and stores the resolved `PropertyInfo` objects in `NodeMap.ModelProperties` and `NodeMap.EntityProperties`, eliminating per-request `Type.GetProperty` calls
3. **Delegate compilation** — `BulkMapper.Compile` builds `Expression`-based getter and setter delegates compiled to IL via `Expression.Lambda.Compile()`, stored in `ConcurrentDictionary` keyed by `TypeFullName.PropertyName`
4. **NodeTree generation** — `NodeTreeIterator.GenerateTree` pre-builds the full traversal tree for every root mapping, so query planning at request time walks a pre-computed structure rather than reflecting over types

By the time the first GraphQL request arrives, the mapping layer has no reflection work left to do. Every property read and write goes through a cached compiled delegate.

### Request-Time Execution

At request time the three phases are:

| Phase | Mechanism | Reflection cost |
|---|---|---|
| SQL generation | Pre-built NodeTree traversal + string assembly | None |
| PostgreSQL execution | Single batched statement via Dapper | None |
| Entity-to-model mapping | `Mapper.MapByAlias` → compiled getter/setter delegates | None |

The `Mapper` uses three `ConcurrentDictionary` caches — `_propCache`, `_getterCache`, `_setterCache` — populated during warmup. `MapByAlias` resolves the `NodeMap` by alias, iterates `FieldMaps`, and copies values using the pre-compiled delegates with no runtime reflection.

---

## Test 1 — Single Customer (`eq` filter)

**Scenario:** One customer with one product per dataset. Fully random data per iteration.

| Metric              | Value  |
|---------------------|--------|
| Datasets            | 5      |
| Iterations Executed | 5      |
| Iterations Failed   | 0      |
| Assertions Executed | 10     |
| Assertions Failed   | 0      |
| Pass Rate           | 100%   |
| Total Duration      | 239 ms |
| Max Response Time   | 67 ms  |
| Avg Response Time   | 13 ms  |

**Per-dataset response times:**

| Dataset   | Response Time |
|-----------|---------------|
| Dataset-1 | 15 ms         |
| Dataset-2 | 13 ms         |
| Dataset-3 | 13 ms         |
| Dataset-4 | 12 ms         |
| Dataset-5 | 14 ms         |

### GraphQL Request

```graphql
mutation a {
  wrapper(
    wrapper: {
      model: INNER_CUSTOMER
      customerCustomerEdge: [
        {
          innerCustomer: {
            customerKey: "{{CustomerKey1}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming1}}"
            fullNaming: "{{FullNaming1}}"
            lastNaming: "{{LastNaming1}}"
            product: [
              {
                accountKey: "{{AccountKey1}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey1}}"
                transactionKey: "{{TransactionKey1}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey1}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        }
      ]
      cacheKey: "2c0c7698-465f-4fbb-a8c1-9614f7ec6c05"
    }
    where: {
      customerCustomerEdge: {
        some: {
          innerCustomer: {
            customerKey: {
              eq: "{{CustomerKey1}}"
            }
          }
        }
      }
    }
  ) {
    edges {
      node {
        customerCustomerEdge {
          innerCustomer {
            customerKey
            customerType
            firstNaming
            fullNaming
            lastNaming
            product {
              contractKey
              accountName
              accountNumber
              amount
              balance
            }
          }
        }
      }
    }
  }
}
```

### Generated SQL

This single GraphQL mutation compiles into **10 upsert statements** followed by **1 SELECT**.

#### Phase 1 — Leaf entity upserts (no FK dependencies)

```sql
INSERT INTO "Lending"."Transaction" ("Amount", "Balance", "TransactionKey")
VALUES ('100', '1200', '9875df6c-42c3-4630-b944-c221de665a66')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "Amount" = EXCLUDED."Amount",
  "Balance" = EXCLUDED."Balance",
  "TransactionKey" = EXCLUDED."TransactionKey";

INSERT INTO "Account"."Account" ("AccountKey", "AccountName", "AccountNumber")
VALUES ('b14ed96f-466e-4176-a7d2-66f9088ac384', '123AN', '321AN')
ON CONFLICT ("AccountKey") DO UPDATE SET
  "AccountKey" = EXCLUDED."AccountKey",
  "AccountName" = EXCLUDED."AccountName",
  "AccountNumber" = EXCLUDED."AccountNumber";

INSERT INTO "Lending"."Contract" ("Amount", "ContractKey", "ContractType")
VALUES ('100', '76dea764-8c7f-474d-98bd-6833d0f92fb5', '0')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "Amount" = EXCLUDED."Amount",
  "ContractKey" = EXCLUDED."ContractKey",
  "ContractType" = EXCLUDED."ContractType";

INSERT INTO "Banking"."CustomerBankingRelationship" ("CustomerBankingRelationshipKey")
VALUES ('d94fbf63-fff5-4523-ba1c-9f12dae2c600')
ON CONFLICT ("CustomerBankingRelationshipKey") DO UPDATE SET
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey";

INSERT INTO "Banking"."Customer" ("CustomerKey", "CustomerType", "FirstName", "FullName", "LastName")
VALUES ('23e8761f-6373-434c-90fb-5359ffa93ff7', '0', 'Cristopher', 'Molly Greenholt', 'Hane')
ON CONFLICT ("CustomerKey") DO UPDATE SET
  "CustomerKey" = EXCLUDED."CustomerKey",
  "CustomerType" = EXCLUDED."CustomerType",
  "FirstName" = EXCLUDED."FirstName",
  "FullName" = EXCLUDED."FullName",
  "LastName" = EXCLUDED."LastName";
```

#### Phase 2 — Relationship resolution upserts (FK stitching via SELECT subqueries)

```sql
-- Resolve Customer → CustomerBankingRelationship
INSERT INTO "Banking"."CustomerBankingRelationship"
  ("CustomerId", "CustomerKey", "CustomerBankingRelationshipKey")
  (SELECT c."Id", c."CustomerKey", 'd94fbf63-fff5-4523-ba1c-9f12dae2c600'
   FROM "Banking"."Customer" c
   WHERE "CustomerKey" = '23e8761f-6373-434c-90fb-5359ffa93ff7')
ON CONFLICT ("CustomerBankingRelationshipKey") DO UPDATE SET
  "CustomerId" = EXCLUDED."CustomerId",
  "CustomerKey" = EXCLUDED."CustomerKey",
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey";

-- Resolve CustomerBankingRelationship → Contract
INSERT INTO "Lending"."Contract"
  ("CustomerBankingRelationshipId", "CustomerBankingRelationshipKey", "ContractKey")
  (SELECT cbr."Id", cbr."CustomerBankingRelationshipKey", '76dea764-8c7f-474d-98bd-6833d0f92fb5'
   FROM "Banking"."CustomerBankingRelationship" cbr
   WHERE "CustomerBankingRelationshipKey" = 'd94fbf63-fff5-4523-ba1c-9f12dae2c600')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "CustomerBankingRelationshipId" = EXCLUDED."CustomerBankingRelationshipId",
  "CustomerBankingRelationshipKey" = EXCLUDED."CustomerBankingRelationshipKey",
  "ContractKey" = EXCLUDED."ContractKey";

-- Resolve Account → Contract
INSERT INTO "Lending"."Contract" ("AccountId", "AccountKey", "ContractKey")
  (SELECT a."Id", a."AccountKey", '76dea764-8c7f-474d-98bd-6833d0f92fb5'
   FROM "Account"."Account" a
   WHERE "AccountKey" = 'b14ed96f-466e-4176-a7d2-66f9088ac384')
ON CONFLICT ("ContractKey") DO UPDATE SET
  "AccountId" = EXCLUDED."AccountId",
  "AccountKey" = EXCLUDED."AccountKey",
  "ContractKey" = EXCLUDED."ContractKey";

-- Resolve Contract → Transaction
INSERT INTO "Lending"."Transaction" ("ContractId", "ContractKey", "TransactionKey")
  (SELECT c."Id", c."ContractKey", '9875df6c-42c3-4630-b944-c221de665a66'
   FROM "Lending"."Contract" c
   WHERE "ContractKey" = '76dea764-8c7f-474d-98bd-6833d0f92fb5')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "ContractId" = EXCLUDED."ContractId",
  "ContractKey" = EXCLUDED."ContractKey",
  "TransactionKey" = EXCLUDED."TransactionKey";

-- Resolve Account → Transaction
INSERT INTO "Lending"."Transaction" ("AccountId", "AccountKey", "TransactionKey")
  (SELECT a."Id", a."AccountKey", '9875df6c-42c3-4630-b944-c221de665a66'
   FROM "Account"."Account" a
   WHERE "AccountKey" = 'b14ed96f-466e-4176-a7d2-66f9088ac384')
ON CONFLICT ("TransactionKey") DO UPDATE SET
  "AccountId" = EXCLUDED."AccountId",
  "AccountKey" = EXCLUDED."AccountKey",
  "TransactionKey" = EXCLUDED."TransactionKey";
```

#### Phase 3 — Single batched SELECT (entire graph, 1 round trip)

```sql
SELECT
  Customer."CustomerKey",
  Customer."CustomerType",
  Customer."FirstName",
  Customer."FullName",
  Customer."LastName",
  CBR."Id"                                    AS "Id____",
  CBR."CustomerId"                            AS "CustomerId____",
  Contract."Id"                               AS "Id_____",
  Contract."CustomerBankingRelationshipId"    AS "CustomerBankingRelationshipId_____",
  Contract."ContractKey"                      AS "ContractKey_____",
  Contract."Amount"                           AS "Amount_____",
  Contract."AccountId"                        AS "AccountId_____",
  Account."Id"                                AS "Id______",
  Account."AccountName"                       AS "AccountName______",
  Account."AccountNumber"                     AS "AccountNumber______",
  Transaction."Id"                            AS "Id_______",
  Transaction."ContractId"                    AS "ContractId_______",
  Transaction."AccountId"                     AS "AccountId_______",
  Transaction."Balance"                       AS "Balance_______"

FROM "Banking"."Customer" Customer

LEFT JOIN (
  SELECT CBR."Id", CBR."CustomerId",
         Contract."Id"                            AS "Id_____",
         Contract."CustomerBankingRelationshipId" AS "CustomerBankingRelationshipId_____",
         Contract."ContractKey"                   AS "ContractKey_____",
         Contract."Amount"                        AS "Amount_____",
         Contract."AccountId"                     AS "AccountId_____",
         Account."Id"                             AS "Id______",
         Account."AccountName"                    AS "AccountName______",
         Account."AccountNumber"                  AS "AccountNumber______",
         Transaction."Id"                         AS "Id_______",
         Transaction."ContractId"                 AS "ContractId_______",
         Transaction."AccountId"                  AS "AccountId_______",
         Transaction."Balance"                    AS "Balance_______"
  FROM "Banking"."CustomerBankingRelationship" CBR
  JOIN (
    SELECT Contract."Id", Contract."CustomerBankingRelationshipId",
           Contract."ContractKey", Contract."Amount", Contract."AccountId",
           Account."Id"            AS "Id______",
           Account."AccountName"   AS "AccountName______",
           Account."AccountNumber" AS "AccountNumber______",
           Transaction."Id"        AS "Id_______",
           Transaction."ContractId" AS "ContractId_______",
           Transaction."AccountId" AS "AccountId_______",
           Transaction."Balance"   AS "Balance_______"
    FROM "Lending"."Contract" Contract
    JOIN (
      SELECT Account."Id", Account."AccountName", Account."AccountNumber",
             Transaction."Id"          AS "Id_______",
             Transaction."ContractId"  AS "ContractId_______",
             Transaction."AccountId"   AS "AccountId_______",
             Transaction."Balance"     AS "Balance_______"
      FROM "Account"."Account" Account
      JOIN (
        SELECT Transaction."Id", Transaction."ContractId",
               Transaction."AccountId", Transaction."Balance"
        FROM "Lending"."Transaction" Transaction
      ) Transaction ON Account."Id" = Transaction."AccountId_______"
    ) Account ON Contract."AccountId" = Account."Id______"
  ) Contract ON CBR."Id" = Contract."CustomerBankingRelationshipId_____"
) CBR ON Customer."Id" = CBR."CustomerId"

WHERE (Customer."CustomerKey" = '23e8761f-6373-434c-90fb-5359ffa93ff7');
```

**Join depth:** 5 tables · 4 JOIN levels · 3 schemas (`Banking`, `Lending`, `Account`) · 1 round trip

### Entity-to-Model Mapping

After the SELECT returns, `QueryHandler.MappingConfiguration` groups rows by root entity key, deduplicates, then calls `Mapper.MapByAlias` for each alias. Because `BulkMapper.Compile` ran at startup, every property read and write goes through a pre-compiled `Expression` delegate — no `Type.GetProperty` or `PropertyInfo.GetValue` calls occur at this stage.

---

## Test 2 — Three Customers (`in` filter, batch)

**Scenario:** Three customers each with one product per dataset. All three upserted and queried in a single GraphQL operation.

| Metric              | Value  |
|---------------------|--------|
| Datasets            | 5      |
| Iterations Executed | 5      |
| Iterations Failed   | 0      |
| Assertions Executed | 30     |
| Assertions Failed   | 0      |
| Pass Rate           | 100%   |
| Total Duration      | 239 ms |
| Max Response Time   | 78 ms  |
| Avg Response Time   | 16 ms  |

**Per-dataset response times:**

| Dataset   | Response Time |
|-----------|---------------|
| Dataset-1 | 14 ms         |
| Dataset-2 | 20 ms         |
| Dataset-3 | 17 ms         |
| Dataset-4 | 14 ms         |
| Dataset-5 | 13 ms         |

### GraphQL Request

```graphql
mutation a {
  wrapper(
    wrapper: {
      model: INNER_CUSTOMER
      cacheKey: "2c0c7698-465f-4fbb-a8c1-9614f7ec6c05"
      customerCustomerEdge: [
        {
          innerCustomer: {
            customerKey: "{{CustomerKey1}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming1}}"
            fullNaming: "{{FullNaming1}}"
            lastNaming: "{{LastNaming1}}"
            product: [
              {
                accountKey: "{{AccountKey1}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey1}}"
                transactionKey: "{{TransactionKey1}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey1}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        },
        {
          innerCustomer: {
            customerKey: "{{CustomerKey2}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming2}}"
            fullNaming: "{{FullNaming2}}"
            lastNaming: "{{LastNaming2}}"
            product: [
              {
                accountKey: "{{AccountKey2}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey2}}"
                transactionKey: "{{TransactionKey2}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey2}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey1}}"
              }
            ]
          }
        },
        {
          innerCustomer: {
            customerKey: "{{CustomerKey3}}"
            customerType: PERSON
            firstNaming: "{{FirstNaming3}}"
            fullNaming: "{{FullNaming3}}"
            lastNaming: "{{LastNaming3}}"
            product: [
              {
                accountKey: "{{AccountKey3}}"
                accountName: "123AN"
                accountNumber: "321AN"
                amount: 100
                balance: 1200
                contractKey: "{{ContractKey3}}"
                transactionKey: "{{TransactionKey3}}"
                productType: CREDIT_CARD
                customerKey: "{{CustomerKey3}}"
                customerBankingRelationshipKey: "{{CustomerBankingRelationshipKey3}}"
              }
            ]
          }
        }
      ]
    }
    where: {
      customerCustomerEdge: {
        some: {
          innerCustomer: {
            customerKey: {
              in: ["{{CustomerKey1}}", "{{CustomerKey2}}", "{{CustomerKey3}}"]
            }
          }
        }
      }
    }
  ) {
    edges {
      node {
        customerCustomerEdge {
          innerCustomer {
            customerKey
            customerType
            firstNaming
            fullNaming
            lastNaming
            product {
              contractKey
              accountName
              accountNumber
              amount
              balance
            }
          }
        }
      }
    }
  }
}
```

### Generated SQL

The three-customer mutation scales the same execution pattern: **30 upsert statements** (10 per customer) followed by the same **1 SELECT** structure with a `WHERE ... IN (...)` clause. The JOIN shape is identical to Test 1 — only the filter changes.

```sql
WHERE (Customer."CustomerKey" IN (
  '{{CustomerKey1}}',
  '{{CustomerKey2}}',
  '{{CustomerKey3}}'
))
```

3× the entities, 3× the upserts, **same single SELECT round trip**. The mapping layer processes 3× the rows using the same pre-compiled delegates with no additional warmup cost.

---

## Observations

- The `Product` model spans **4 physical tables** across 3 PostgreSQL schemas. In a resolver-chain GraphQL implementation this relationship alone would trigger N+1 queries at every nesting level. Coffee Beanery compiles the entire graph into **1 SELECT** regardless of entity count or depth.
- All property access in the mapping layer uses **pre-compiled `Expression` delegates** (populated by `BulkMapper.Compile` at startup), eliminating reflection overhead from the hot path entirely.
- Scaling from 1 to 3 customers (3× entities, 3× upserts, 3× assertions) added only **3 ms** to average response time (13 ms → 16 ms). Total end-to-end duration remained **identical at 239 ms**.
- Max response time increased by only **11 ms** (67 ms → 78 ms) when handling 3× the data.
- **0 assertion failures** across all 40 assertions (10 + 30) on fully randomized UUID data.

---

## Environment Notes

- No application-level or HTTP caching active during tests
- PostgreSQL built-in execution plan cache active (plans reused after first execution)
- Mapping warmup (property cache + delegate compilation + NodeTree generation) runs once at startup before any request is served
- All keys (customer, account, contract, transaction, banking relationship) are random UUIDs per dataset
- All name fields (first, full, last) are random strings per dataset
- Schemas involved: `Banking`, `Lending`, `Account`
- Relationship traversal depth: `Wrapper → CustomerCustomerEdge → InnerCustomer → Product (Contract + Account + Transaction)`

---

## Related Documentation

- [Native AOT](Native-AOT.md)
- [Persistence → Caching](../08-Persistence/Caching.md)
- [Runtime → Execution](../04-Runtime/Execution.md)

---

← Previous: [Native AOT](Native-AOT.md)  |  Next: [Samples](../11-Samples/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Performance](README.md) → **Native AOT**

# Native AOT

## Contents

- [Why Native AOT?](#why-native-aot)
- [Design Principles](#design-principles)
- [Dynamic Features to Avoid](#dynamic-features-to-avoid)
- [Testing Native AOT](#testing-native-aot)
- [Performance Benefits](#performance-benefits)

---

## Why Native AOT?

Native AOT provides:

- Faster startup
- Lower memory usage
- Smaller deployment footprint
- Better container performance
- Reduced cold-start latency
- Improved cloud scalability

Supporting Native AOT also encourages better architectural discipline.

---

## Design Principles

CoffeeBeanery achieves Native AOT compatibility by avoiding runtime features that require dynamic analysis.

Key principles include:

- No runtime reflection
- No runtime code generation
- No expression compilation
- No dynamic proxy generation
- No runtime metadata discovery

Everything required for execution is generated during compilation.

---

## Dynamic Features to Avoid

Avoid introducing:

- Reflection
- DynamicMethod
- Reflection.Emit
- Expression.Compile()
- Runtime IL generation
- Dynamic proxies
- Runtime assembly scanning

These features either reduce AOT compatibility or require additional configuration.

---

## Collections

Prefer static, immutable collections.

Examples:

```csharp
ImmutableArray<T>

ImmutableDictionary<TKey, TValue>
```

Generated metadata should be initialized once and reused for the application's lifetime.

---

## Generic Code

Prefer closed generic registrations where practical.

Avoid runtime generic construction using reflection.

Generated registries should reference concrete implementations directly.

---

## Serialization

Where serialization is required, prefer source-generated serializers.

Example:

```csharp
[JsonSerializable(typeof(Customer))]
internal partial class CoffeeBeaneryJsonContext
    : JsonSerializerContext
{
}
```

Avoid reflection-based serializers.

---

## SQL

SQL generation should remain purely deterministic.

SQL writers should consume immutable execution plans without inspecting CLR types.

This naturally aligns with Native AOT constraints.

---

## Testing Native AOT

Native AOT should be validated continuously.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Query execution
- Mutation execution
- Materialization
- Metadata resolution

These tests help prevent accidental introduction of unsupported runtime features.

---

## Performance Benefits

Designing for Native AOT also improves traditional JIT execution.

Benefits include:

- Fewer allocations
- Reduced startup work
- Simpler execution paths
- Better cache locality
- More predictable performance

Compile-time optimization benefits every deployment model.

---

---

## Related Documentation

- [Benchmarks](Benchmarks.md)
- [Source Generators](../06-Source-Generators/README.md)
- [Foundation → Components](../03-Foundation/Components.md)

---

← Previous: [Performance](README.md)  |  Next: [Benchmarks](Benchmarks.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Samples**

# Samples

## Contents

- [The Banking sample](#the-banking-sample)
- [Solution layout](#solution-layout)
- [Running it](#running-it)

---

## The Banking sample

`example/HotChocolateCoffeeBeanery` is the one sample in the repository today, and it's the
canonical reference for every layer of Coffee Beanery working together: EF Core mapping
classes, the [mapping generator](../06-Source-Generators/Mapping-Generator.md), the
[runtime](../04-Runtime/README.md), [Hot Chocolate](../05-GraphQL/README.md), and
[PostgreSQL + Apache AGE](../08-Persistence/PostgreSQL-AGE.md), modeling a small Banking
domain (customers, accounts, contracts, transactions).

It uses:

- Dapper
- Hot Chocolate
- Entity Framework (as the mapping source, not the execution engine — see
  [Persistence → Dapper & EF Core](../08-Persistence/Dapper-EFCore.md))
- PostgreSQL
- FasterKV (in-process cache)

## Solution layout

| Project | Role |
|---|---|
| `Api/Api.Banking` | ASP.NET Core host, GraphQL endpoint, query/mutation resolvers |
| `Domain/CoffeeBeanery` | The framework runtime |
| `Domain/CoffeeBeanery.GraphQL.Core.Foundation` | Foundation contracts |
| `Domain/CoffeeBeanery.GraphQL.Core.Mapping.Generators` | The Roslyn mapping generator |
| `Domain/Domain.Model`, `Domain/Domain.Shared` | Business/domain model and mapping DSL |
| `Infrastructure/Command` | Command-side infrastructure |
| `Infrastructure/Database/Database.Entity*` | EF Core entity models + migrations (relational) |
| `Infrastructure/Database/Database.Graph*` | Apache AGE graph models + migrations |
| `Test` | Test project |

## Running it

See [Getting Started → Installation](../01-Getting-Started/Installation.md) and
[Getting Started → First Service](../01-Getting-Started/First-Service.md) for the full
walkthrough, including PostgreSQL/AGE setup.

---

## Related Documentation

- [Getting Started](../01-Getting-Started/README.md)
- [GraphQL](../05-GraphQL/README.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Performance](../10-Performance/README.md)  |  Next: [Contributing](../12-Contributing/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Contributing**

# Contributing

## Contents

- [Code Style](Code-Style.md)
- [Testing](Testing.md)
- [ADR Process](ADR-Process.md)

---

## Philosophy

## Philosophy

CoffeeBeanery is built around a few simple principles.

- Compile-time first
- Immutable by default
- Native AOT friendly
- Transport agnostic
- Dependency inversion
- Single responsibility
- Explicit architecture

Every contribution should reinforce these principles.

---

## Before Contributing

## Before Contributing

Before opening a Pull Request, contributors should read:

- Architecture.md
- Foundation.md
- Runtime.md
- SQL.md
- Generator.md
- Planning.md
- ADR.md

Understanding the architecture is far more important than understanding individual implementations.

---

## Development Workflow

## Development Workflow

Recommended workflow:

```
Fork Repository

↓

Create Branch

↓

Implement Feature

↓

Run Tests

↓

Run Generator Tests

↓

Open Pull Request

↓

Review

↓

Merge
```

Every Pull Request should focus on one logical change.

---

## Pull Requests

## Pull Requests

A good Pull Request should:

- Solve one problem
- Include tests
- Preserve architecture
- Keep commits focused
- Explain architectural impact

Large unrelated changes should be split into multiple PRs.

---

## Review Checklist

Before approving a Pull Request, reviewers should verify:

- Correct dependency direction
- No runtime reflection
- No architectural boundary violations
- Tests updated
- Documentation updated
- Generated output remains deterministic
- Native AOT compatibility preserved

---

---

## Related Documentation

- [Code Style](Code-Style.md)
- [Testing](Testing.md)
- [Reference → ADRs](../13-Reference/ADRs.md)

---

← Previous: [Samples](../11-Samples/README.md)  |  Next: [Reference](../13-Reference/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **ADR Process**

# ADR Process

## Contents

- [When to write an ADR](#when-to-write-an-adr)
- [Format](#format)
- [Where ADRs live](#where-adrs-live)
- [Review](#review)

---

## When to write an ADR

Write an Architecture Decision Record before making a change that:

- Alters a dependency direction between layers (see [Architecture → Dependency Graph](../02-Architecture/Dependency-Graph.md))
- Adds or changes a Foundation contract (see [Foundation → Contracts](../03-Foundation/Contracts.md))
- Adds a new execution provider or transport (see [Architecture → Vision](../02-Architecture/Vision.md#roadmap-by-phase))
- Changes how the source generator discovers or validates mappings

Small implementation details, bug fixes, and refactors that don't change a public contract
don't need one.

## Format

Follow the existing ADRs in [Reference → ADRs](../13-Reference/ADRs.md) as a template:
**Status**, **Context**, **Decision**, **Consequences** (split into Advantages and
Trade-offs). Keep each ADR focused on one decision.

## Where ADRs live

All accepted ADRs live in one place: [`docs/13-Reference/ADRs.md`](../13-Reference/ADRs.md),
numbered sequentially (`ADR-013`, `ADR-014`, ...). Don't create standalone ADR files
scattered across sections — a single, append-only list is what makes the decision history
searchable.

## Review

Open a pull request with the new ADR appended, following the
[review checklist](README.md#pull-requests). An ADR should be reviewed and merged (Status:
Accepted) before the change it describes is implemented, not after.

---

## Related Documentation

- [Reference → ADRs](../13-Reference/ADRs.md)
- [Contributing](README.md)
- [Architecture](../02-Architecture/README.md)

---

← Previous: [Testing](Testing.md)  |  Next: [Reference](../13-Reference/README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **Code Style**

# Code Style

## Contents

- [General Principles](#general-principles)
- [Architecture First](#architecture-first)
- [File Organization](#file-organization)
- [Naming](#naming)
- [Method Size](#method-size)
- [Immutability](#immutability)
- [Exceptions & Pattern Matching](#exceptions--pattern-matching)
- [Comments](#comments)

---

## General Principles

Code should be:

- Readable
- Predictable
- Deterministic
- Testable
- Allocation-conscious
- Native AOT friendly

When faced with two implementations of equal performance, always choose the simpler one.

---

## Architecture First

Every implementation should respect project boundaries.

```
Foundation

↑

Runtime

↑

GraphQL
```

Never introduce shortcuts that violate dependency direction.

Architectural consistency is more important than reducing a few lines of code.

---

## File Organization

One public type per file.

Example:

```
EntityMetadata.cs

QueryPlanner.cs

GeneratedMetadataProvider.cs
```

Avoid grouping unrelated public types in the same file.

---

## Naming

Names should describe intent.

Prefer:

```csharp
ResolveJoinMetadata()

BuildMutationPlan()

WriteConflictClause()
```

Instead of:

```csharp
Resolve()

Build()

Write()
```

Variables should also be descriptive.

Good:

```csharp
entityMetadata

columnReference

joinMetadata
```

Avoid:

```csharp
x

tmp

obj

data
```

---

## Method Size

Methods should generally perform one logical task.

Large methods should be decomposed into private helpers.

Instead of:

```
BuildEverything()
```

Prefer:

```
ResolveMetadata()

BuildProjection()

BuildOrdering()

BuildFilters()
```

Small methods are easier to understand and test.

---

## Immutability

Prefer immutable types.

Example:

```csharp
public sealed class EntityMetadata
{
    public ushort Id { get; }

    public string Name { get; }

    public ImmutableArray<ColumnMetadata> Columns { get; }
}
```

Mutable state should be limited to execution-specific objects.

---

## Exceptions

Throw exceptions only for exceptional situations.

Validation errors should occur during planning or generation whenever possible.

Runtime should rarely encounter invalid metadata.

---

## Comments

Comments should explain **why**, not **what**.

Good:

```csharp
// Preserve deterministic alias ordering for snapshot stability.
```

Avoid:

```csharp
// Increment i.
i++;
```

Code should be self-explanatory whenever possible.

---

---

## Related Documentation

- [Contributing](README.md)
- [Architecture → Principles](../02-Architecture/Principles.md)
- [Testing](Testing.md)

---

← Previous: [Contributing](README.md)  |  Next: [Testing](Testing.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Contributing](README.md) → **Testing**

# Testing

## Contents

- [Philosophy](#philosophy)
- [Testing Pyramid](#testing-pyramid)
- [Generator Tests](#generator-tests)
- [Runtime Tests](#runtime-tests)
- [Native AOT Tests](#native-aot-tests)
- [Continuous Integration](#continuous-integration)

---

## Philosophy

Testing should mirror the architecture.

```
Foundation

↓

Generator

↓

Runtime

↓

SQL

↓

Transport
```

Every layer has its own responsibilities and should be tested independently.

Avoid relying solely on end-to-end integration tests.

---

## Testing Pyramid

```
               End-to-End
            Integration Tests
             Snapshot Tests
               Unit Tests
```

The majority of tests should be unit tests.

Integration tests validate interactions between components.

Snapshot tests validate generated code.

---

## Generator Tests

The Generator requires the largest test surface.

Recommended categories:

```
Parser Tests

↓

Validation Tests

↓

Relationship Tests

↓

Identifier Allocation Tests

↓

Metadata Generation Tests

↓

Snapshot Tests
```

Each stage should be tested independently.

---

## Parser Tests

Parser tests verify discovery of application models.

Example scenarios:

- Entity detection
- Property discovery
- Graph discovery
- Join discovery
- Lookup discovery

Parser tests should isolate Roslyn analysis from code generation.

---

## Runtime Tests

Runtime tests verify execution behavior independently of SQL.

Examples:

- Query execution
- Mutation execution
- Dependency ordering
- Generated value propagation
- Materialization coordination
- Transaction handling

Runtime tests should replace external dependencies with test doubles where practical.

---

## Native AOT Tests

Because Native AOT is a core design goal, compatibility should be validated regularly.

Recommended checks:

- Successful AOT compilation
- Runtime execution
- Generated materializers
- Metadata provider
- Planner registry

No runtime reflection should be introduced.

---

## Continuous Integration

Every pull request should execute:

- Unit tests
- Generator tests
- Snapshot tests
- Integration tests
- Native AOT validation (where supported)

Builds should fail if generated snapshots change unexpectedly.

---

---

## Related Documentation

- [Code Style](Code-Style.md)
- [Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md)
- [Performance → Native AOT](../10-Performance/Native-AOT.md)

---

← Previous: [Code Style](Code-Style.md)  |  Next: [ADR Process](ADR-Process.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → **Reference**

# Reference

## Contents

- [ADRs](ADRs.md) — Architecture Decision Records
- [FAQ](FAQ.md) — extended architecture FAQ
- [Glossary](Glossary.md) — terminology used throughout this documentation set
- [Roadmap](Roadmap.md) — the phased plan from Phase 1 to long-term vision
- [Changelog](Changelog.md)
- [Migration Guides](Migration-Guides.md)

---

## Related Documentation

- [Architecture](../02-Architecture/README.md)
- [Contributing → ADR Process](../12-Contributing/ADR-Process.md)

---

← Previous: [Contributing](../12-Contributing/README.md)  |  Next: [ADRs](ADRs.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **ADRs**

# Architecture Decision Records

Twelve foundational ADRs, recorded when the compile-time-first architecture was adopted.
New ADRs are appended here — see [Contributing → ADR Process](../12-Contributing/ADR-Process.md)
before proposing one.

## Contents

- [ADR-001 — Compile-Time First Architecture](#adr-001-compile-time-first-architecture)
- [ADR-002 — Foundation Owns Contracts](#adr-002-foundation-owns-contracts)
- [ADR-003 — Runtime Depends on Interfaces](#adr-003-runtime-depends-on-interfaces)
- [ADR-004 — Immutable Metadata](#adr-004-immutable-metadata)
- [ADR-005 — Immutable Execution Plans](#adr-005-immutable-execution-plans)
- [ADR-006 — Generated Materializers](#adr-006-generated-materializers)
- [ADR-007 — SQL Is a Serialization Layer](#adr-007-sql-is-a-serialization-layer)
- [ADR-008 — GraphQL Is a Transport](#adr-008-graphql-is-a-transport)
- [ADR-009 — Dependency Inversion for Generated Code](#adr-009-dependency-inversion-for-generated-code)
- [ADR-010 — Stable Identifier Allocation](#adr-010-stable-identifier-allocation)
- [ADR-011 — Transport Independence](#adr-011-transport-independence)
- [ADR-012 — Native AOT Compatibility](#adr-012-native-aot-compatibility)
- [Summary](#summary)

---

> This document records the major architectural decisions that shape the CoffeeBeanery framework. It is intended to provide context for contributors and future maintainers, explaining not only **what** the architecture is, but **why** specific design choices were made.

---

## ADR-001 — Compile-Time First Architecture

### Status

Accepted

### Context

Traditional GraphQL frameworks perform extensive runtime analysis using reflection, expression trees, and dynamic code generation. This increases startup time, memory usage, and complexity while limiting compatibility with Native AOT.

### Decision

CoffeeBeanery moves as much work as possible from runtime to compile time using Roslyn Incremental Source Generators.

Compilation is responsible for:

- Metadata discovery
- Relationship analysis
- Identifier allocation
- Materializer generation
- Dematerializer generation
- Planner registry generation
- Runtime registrations

Runtime executes precomputed artifacts.

### Consequences

### Advantages

- Faster startup
- Native AOT compatibility
- Reduced allocations
- Deterministic execution
- Simpler runtime

### Trade-offs

- More complex generator
- Larger generated code
- Increased compile-time work

---

## ADR-002 — Foundation Owns Contracts

### Status

Accepted

### Context

Runtime, SQL, GraphQL, and generated code require a common vocabulary.

Without a dedicated foundation layer, dependencies become cyclic and implementations leak across project boundaries.

### Decision

Foundation defines:

- Metadata
- Planning primitives
- Interfaces
- Runtime primitives
- Identifiers

Foundation references no other CoffeeBeanery project.

### Consequences

Every project shares the same contracts while remaining loosely coupled.

---

## ADR-003 — Runtime Depends on Interfaces

### Status

Accepted

### Context

The original Runtime directly referenced generated static classes such as:

```csharp
GeneratedMetadata.GetEntity(...)
```

This tightly coupled Runtime to generated code.

### Decision

Runtime depends on abstractions instead.

Example:

```csharp
IMetadataProvider
```

implemented by:

```csharp
GeneratedMetadataProvider
```

### Consequences

Generated code becomes a plug-in rather than a dependency.

Runtime becomes reusable across transports.

---

## ADR-004 — Immutable Metadata

### Status

Accepted

### Context

Runtime repeatedly consumes metadata.

Mutable metadata increases complexity and thread-safety concerns.

### Decision

Every metadata object is immutable.

Examples include:

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata

Metadata is created once and shared for the application's lifetime.

### Consequences

- Thread-safe
- Singleton lifetime
- Predictable behavior
- Easier testing

---

## ADR-005 — Immutable Execution Plans

### Status

Accepted

### Context

Execution should not modify planning decisions.

### Decision

QueryPlan and MutationPlan are immutable.

Planning performs analysis.

Runtime performs execution.

### Consequences

Runtime becomes deterministic and easier to reason about.

---

## ADR-006 — Generated Materializers

### Status

Accepted

### Context

Reflection-based materialization is slower and incompatible with Native AOT.

### Decision

The Generator emits dedicated materializers for every model.

Runtime invokes generated materializers directly.

### Consequences

- No reflection
- Better performance
- Easier debugging
- Native AOT compatibility

---

## ADR-007 — SQL Is a Serialization Layer

### Status

Accepted

### Context

Planning determines execution semantics.

SQL should not duplicate planning logic.

### Decision

SQL converts immutable plans into dialect-specific SQL.

It performs no metadata discovery or semantic analysis.

### Consequences

Clear separation between planning and serialization.

---

## ADR-008 — GraphQL Is a Transport

### Status

Accepted

### Context

GraphQL frameworks often mix transport concerns with execution.

### Decision

GraphQL only:

- Builds schemas
- Parses requests
- Invokes planners
- Calls Runtime

Execution occurs entirely within Runtime.

### Consequences

The same Runtime can support GraphQL, gRPC, Web API, and future transports.

---

## ADR-009 — Dependency Inversion for Generated Code

### Status

Accepted

### Context

Generated code should not dictate Runtime architecture.

### Decision

Generated implementations satisfy Foundation interfaces.

Examples include:

- IMetadataProvider
- IPlannerRegistry
- IEntityMaterializer
- IEntityDematerializer

### Consequences

Generated code becomes replaceable and testable.

---

## ADR-010 — Stable Identifier Allocation

### Status

Accepted

### Context

Changing identifier values unnecessarily creates noisy diffs and instability.

### Decision

Identifiers are allocated deterministically after validation.

Allocation order should remain stable between builds unless the model changes.

### Consequences

Cleaner generated code and more predictable version control history.

---

## ADR-011 — Transport Independence

### Status

Accepted

### Context

CoffeeBeanery is intended to support multiple client technologies.

### Decision

Runtime and SQL remain transport agnostic.

GraphQL, gRPC, and Web API become thin adapters over the same execution engine.

### Consequences

New transports can be introduced without modifying Runtime.

---

## ADR-012 — Native AOT Compatibility

### Status

Accepted

### Context

Native AOT imposes restrictions on reflection and runtime code generation.

### Decision

CoffeeBeanery avoids:

- Reflection
- Expression compilation
- Runtime metadata discovery
- Dynamic proxy generation

Generated code replaces these mechanisms.

### Consequences

Applications remain compatible with Native AOT while retaining high performance.

---

## Summary

These architectural decisions establish the core principles of CoffeeBeanery:

- Compile-time first
- Immutable metadata
- Immutable execution plans
- Dependency inversion
- Transport independence
- Generated implementations
- Native AOT compatibility
- Clear project boundaries

Future architectural changes should be evaluated against these principles to preserve the framework's long-term consistency and maintainability.

---

## Related Documentation

- [Architecture](../02-Architecture/README.md)
- [Contributing → ADR Process](../12-Contributing/ADR-Process.md)
- [Roadmap](Roadmap.md)

---

← Previous: [Reference](README.md)  |  Next: [FAQ](FAQ.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Changelog**

# Changelog

## Contents

- [Unreleased](#unreleased)
- [Format](#format)

---

## Unreleased

Coffee Beanery has not yet cut a tagged, versioned release — Phase 1 (EF Core mapping +
Hot Chocolate + PostgreSQL + Dapper, see [Vision](../02-Architecture/Vision.md)) is under
active development against the `main` branch. This page is a placeholder for the moment
there's a first tagged version, rather than a fabricated version history.

Notable recent documentation work:

- Restructured `docs/` from a flat, duplicated set of files into the 13-section framework
  described at [Documentation Home](../README.md).
- Regenerated `README.md`, `llms.txt`, `llms-full.md`, and `AI.SEO.md` to match.
- Archived the previous, duplicated documentation set under
  [`docs/archive`](archive/README.md) for history.

## Format

Once a first version is tagged, this page will follow
[Keep a Changelog](https://keepachangelog.com/) conventions (`Added` / `Changed` /
`Deprecated` / `Removed` / `Fixed` / `Security`, newest first), and releases will be tagged
following [Semantic Versioning](https://semver.org/).

---

## Related Documentation

- [Roadmap](Roadmap.md)
- [Migration Guides](Migration-Guides.md)
- [ADRs](ADRs.md)

---

← Previous: [Roadmap](Roadmap.md)  |  Next: [Migration Guides](Migration-Guides.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **FAQ**

# FAQ

Extended architecture FAQ. For first-hour setup questions, see
[Getting Started → FAQ](../01-Getting-Started/FAQ.md).

## Contents

- [Why does CoffeeBeanery use Source Generators?](#why-does-coffeebeanery-use-source-generators)
- [Why not use reflection?](#why-not-use-reflection)
- [Why split Foundation from Runtime?](#why-split-foundation-from-runtime)
- [Why generate metadata?](#why-generate-metadata)
- [Why immutable metadata?](#why-immutable-metadata)
- [Why immutable execution plans?](#why-immutable-execution-plans)
- [Why separate Planning from SQL?](#why-separate-planning-from-sql)
- [Why generate materializers?](#why-generate-materializers)
- [Why not build SQL inside GraphQL?](#why-not-build-sql-inside-graphql)
- [Why support multiple transports?](#why-support-multiple-transports)
- [Why use Dependency Injection?](#why-use-dependency-injection)
- [Why avoid static generated classes?](#why-avoid-static-generated-classes)
- [Why does Runtime avoid Roslyn?](#why-does-runtime-avoid-roslyn)
- [Why prioritize Native AOT?](#why-prioritize-native-aot)
- [Can CoffeeBeanery support databases other than PostgreSQL?](#can-coffeebeanery-support-databases-other-than-postgresql)
- [Can CoffeeBeanery support transports other than GraphQL?](#can-coffeebeanery-support-transports-other-than-graphql)
- [Why generate identifiers?](#why-generate-identifiers)
- [What belongs in Foundation?](#what-belongs-in-foundation)
- [What belongs in Runtime?](#what-belongs-in-runtime)
- [What belongs in the Generator?](#what-belongs-in-the-generator)
- [What makes CoffeeBeanery different?](#what-makes-coffeebeanery-different)
- [Summary](#summary)

---

> This document answers the most common questions about CoffeeBeanery's architecture, design decisions, and development philosophy.

---

## Why does CoffeeBeanery use Source Generators?

CoffeeBeanery performs most framework analysis during compilation rather than runtime.

This includes:

- Metadata generation
- Relationship analysis
- Identifier allocation
- Materializer generation
- Planner generation
- Runtime registrations

This significantly reduces runtime work while improving startup performance and Native AOT compatibility.

---

## Why not use reflection?

Reflection introduces:

- Startup overhead
- Additional allocations
- Dynamic behavior
- Native AOT limitations
- Runtime uncertainty

Generated code provides the same information without requiring runtime discovery.

---

## Why split Foundation from Runtime?

Foundation defines contracts.

Runtime implements behavior.

Keeping them separate provides:

- Stable interfaces
- Better testing
- Dependency inversion
- Transport independence
- Cleaner project references

Foundation should never know Runtime exists.

---

## Why generate metadata?

Metadata rarely changes while an application is running.

Generating metadata once during compilation avoids repeated runtime analysis and enables immutable, singleton metadata objects.

---

## Why immutable metadata?

Immutable metadata is:

- Thread-safe
- Reusable
- Predictable
- Easy to cache
- Easy to test

Runtime never needs to modify metadata.

---

## Why immutable execution plans?

Planning determines *what* should happen.

Execution determines *when* it happens.

Separating these responsibilities simplifies Runtime and improves determinism.

---

## Why separate Planning from SQL?

Planning understands application semantics.

SQL understands database syntax.

Keeping them independent allows:

- Better testing
- Multiple SQL dialects
- Cleaner architecture
- Simpler SQL writers

---

## Why generate materializers?

Generated materializers:

- Avoid reflection
- Read values by ordinal
- Reduce allocations
- Improve performance
- Support Native AOT

Materialization becomes simple generated code.

---

## Why not build SQL inside GraphQL?

GraphQL is a transport.

Its responsibilities are:

- Schema
- Resolvers
- Request parsing

SQL belongs to the SQL project.

Keeping transport and execution separate makes Runtime reusable.

---

## Why support multiple transports?

The same execution engine should support:

- GraphQL
- gRPC
- REST
- CLI
- Background workers

Only the request translation changes.

Execution remains identical.

---

## Why use Dependency Injection?

Dependency Injection allows Runtime to depend upon interfaces rather than generated implementations.

For example:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

This improves testing and extensibility.

---

## Why avoid static generated classes?

Static classes tightly couple Runtime to generated code.

Generated implementations registered through interfaces allow:

- Mocking
- Replacement
- Testing
- Multiple implementations

---

## Why does Runtime avoid Roslyn?

Roslyn is a compile-time technology.

Runtime should execute plans—not analyze source code.

Keeping Roslyn isolated within the Generator reduces complexity and improves portability.

---

## Why prioritize Native AOT?

Native AOT aligns naturally with CoffeeBeanery's architecture.

Compile-time generation eliminates the need for:

- Reflection
- Dynamic proxies
- Runtime code generation
- Expression compilation

The resulting framework performs well in both JIT and AOT environments.

---

## Can CoffeeBeanery support databases other than PostgreSQL?

Yes.

Planning is database-independent.

Only SQL serialization changes.

Future providers may include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

---

## Can CoffeeBeanery support transports other than GraphQL?

Yes.

Runtime is transport agnostic.

Future transports may include:

- gRPC
- REST
- SignalR
- CLI
- Batch processing

---

## Why generate identifiers?

Stable generated identifiers provide:

- Fast lookups
- Array indexing
- Deterministic output
- Smaller runtime overhead

Identifiers are allocated during compilation.

---

## What belongs in Foundation?

Foundation contains:

- Metadata
- Interfaces
- Planning primitives
- Runtime primitives
- Identifiers

It intentionally excludes:

- Runtime
- SQL
- Roslyn
- GraphQL
- Generated code

---

## What belongs in Runtime?

Runtime owns:

- Query execution
- Mutation execution
- Transaction coordination
- Materialization orchestration
- Execution pipelines

Runtime should never:

- Discover metadata
- Parse attributes
- Generate SQL
- Inspect CLR models

---

## What belongs in the Generator?

The Generator performs compile-time work:

- Model discovery
- Validation
- Relationship resolution
- Metadata generation
- Materializer generation
- Planner generation
- Runtime registration generation

Generated code becomes Runtime's input.

---

## What makes CoffeeBeanery different?

CoffeeBeanery differs from many data frameworks by emphasizing:

- Compile-time analysis
- Immutable metadata
- Immutable execution plans
- Source generation
- Transport independence
- Dependency inversion
- Native AOT compatibility

The framework is designed so Runtime executes precomputed artifacts rather than discovering application structure during execution.

---

## Summary

CoffeeBeanery's design choices consistently favor compile-time computation, immutable models, clear architectural boundaries, and reusable execution components.

Understanding these principles makes the rest of the framework significantly easier to understand and extend.

---

## Related Documentation

- [ADRs](ADRs.md)
- [Glossary](Glossary.md)
- [Architecture → Vision](../02-Architecture/Vision.md)

---

← Previous: [ADRs](ADRs.md)  |  Next: [Glossary](Glossary.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Glossary**

# Glossary

## Contents

- [Entity](#entity)
- [Model](#model)
- [Storage Entity](#storage-entity)
- [Field](#field)
- [Column](#column)
- [Column Reference](#column-reference)
- [Metadata](#metadata)
- [Metadata Provider](#metadata-provider)
- [Query](#query)
- [Mutation](#mutation)
- [Query Planner](#query-planner)
- [Mutation Planner](#mutation-planner)
- [Query Plan](#query-plan)
- [Mutation Plan](#mutation-plan)
- [Materializer](#materializer)
- [Dematerializer](#dematerializer)
- [Planner Registry](#planner-registry)
- [Graph](#graph)
- [Join](#join)
- [Graph Strategy](#graph-strategy)
- [SQL Writer](#sql-writer)
- [SQL Dialect](#sql-dialect)
- [Generator](#generator)
- [Generated Code](#generated-code)
- [Foundation](#foundation)
- [Runtime](#runtime)
- [SQL Layer](#sql-layer)
- [Transport](#transport)
- [Dependency Inversion](#dependency-inversion)
- [Native AOT](#native-aot)
- [Summary](#summary)

---

> This glossary defines the core terminology used throughout the CoffeeBeanery framework. The terms below have specific architectural meanings and should be used consistently across documentation, code, and discussions.

---

## Entity

A physical storage object.

An Entity represents the database structure rather than the public application model.

Examples include:

- Customer
- Order
- Product

Entities map directly to storage metadata.

---

## Model

A public CLR representation exposed to the application.

A model may map to:

- one entity
- multiple entities
- graph traversals
- projections

Models are transport-independent.

---

## Storage Entity

The physical table or storage object used by SQL generation.

Every storage entity has:

- StorageEntityId
- EntityMetadata
- Columns
- Keys

---

## Field

A logical member exposed by a model.

Fields may represent:

- database columns
- computed values
- graph properties
- projections
- aggregates

Fields are not always physical columns.

---

## Column

A physical database column.

Columns belong to storage entities.

Example:

```
Customer.Name
```

becomes

```
Customer

↓

Name Column
```

---

## Column Reference

A lightweight object describing a physical column.

Typically includes:

- Entity
- Column Metadata
- Ordinal
- Identifier

Column references eliminate repeated metadata lookups.

---

## Metadata

Immutable information describing the application's data model.

Examples include:

- EntityMetadata
- ModelMetadata
- JoinMetadata
- GraphMetadata
- ColumnMetadata

Metadata is generated during compilation.

---

## Metadata Provider

The runtime source of metadata.

Example:

```csharp
IMetadataProvider
```

Typical implementation:

```csharp
GeneratedMetadataProvider
```

Runtime depends only upon the interface.

---

## Query

A read operation.

Queries produce immutable QueryPlans which Runtime executes.

---

## Mutation

A write operation.

Mutations produce immutable MutationPlans containing dependency information and SQL operations.

---

## Query Planner

Converts selections into immutable QueryPlans.

Responsibilities include:

- projection planning
- join planning
- graph planning
- metadata resolution

---

## Mutation Planner

Converts mutation requests into immutable MutationPlans.

Responsibilities include:

- dependency analysis
- relationship resolution
- ordering
- generated value propagation

---

## Query Plan

An immutable description of a read operation.

Contains everything Runtime requires for execution.

---

## Mutation Plan

An immutable description of a write operation.

Includes:

- operations
- dependencies
- projections
- graph merges

---

## Materializer

Generated code that converts database rows into CLR objects.

Materializers replace reflection.

---

## Dematerializer

Generated code that converts CLR objects into mutation values.

Dematerializers replace runtime property inspection.

---

## Planner Registry

A generated registry that maps identifiers to planners.

Runtime uses the registry to locate generated planners without reflection.

---

## Graph

A graph representation consisting of:

- vertices
- edges
- traversals

Graph metadata exists independently of relational metadata.

---

## Join

A relationship between two storage entities.

Joins are resolved during planning rather than SQL generation.

---

## Graph Strategy

A provider responsible for graph-specific SQL generation.

Example implementations:

- Apache AGE
- Custom graph providers

---

## SQL Writer

A component that converts immutable execution plans into executable SQL statements.

SQL Writers do not perform planning.

---

## SQL Dialect

Defines database-specific SQL syntax.

Examples include:

- PostgreSQL
- SQL Server
- SQLite

Only serialization changes between dialects.

---

## Generator

The Roslyn Incremental Source Generator responsible for compile-time analysis and code generation.

The Generator produces:

- metadata
- identifiers
- planners
- materializers
- registrations

---

## Generated Code

Source emitted during compilation.

Generated code should contain:

- immutable metadata
- registrations
- strongly typed implementations

Generated code should contain very little business logic.

---

## Foundation

The lowest architectural layer.

Defines:

- contracts
- metadata
- identifiers
- planning primitives

Foundation references no other CoffeeBeanery project.

---

## Runtime

The execution engine.

Consumes:

- metadata
- planners
- SQL writers
- materializers

Runtime performs execution only.

---

## SQL Layer

The serialization layer responsible for converting execution plans into SQL.

It does not understand GraphQL or CLR models.

---

## Transport

An adapter that translates external requests into Runtime plans.

Examples include:

- GraphQL
- gRPC
- REST

Transports do not execute queries.

---

## Dependency Inversion

An architectural principle where Runtime depends upon interfaces rather than generated implementations.

Example:

```
Runtime

↓

IMetadataProvider

↓

GeneratedMetadataProvider
```

This keeps Runtime reusable and testable.

---

## Native AOT

Ahead-of-Time compilation supported through compile-time generation and the avoidance of runtime reflection.

CoffeeBeanery is designed to remain fully compatible with Native AOT.

---

## Summary

Using this terminology consistently helps maintain a shared architectural language across the CoffeeBeanery codebase. Contributors should prefer these definitions when naming types, writing documentation, reviewing pull requests, and discussing future design decisions.

---

## Related Documentation

- [FAQ](FAQ.md)
- [Architecture](../02-Architecture/README.md)
- [Foundation](../03-Foundation/README.md)

---

← Previous: [FAQ](FAQ.md)  |  Next: [Roadmap](Roadmap.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Migration Guides**

# Migration Guides

## Contents

- [No versioned releases yet](#no-versioned-releases-yet)
- [Adopting the mapping generator](#adopting-the-mapping-generator)

---

## No versioned releases yet

As noted in the [Changelog](Changelog.md), there's no tagged release history yet, so there
are no version-to-version migration guides in the traditional sense. This page will grow one
entry per breaking change once releases start shipping.

## Adopting the mapping generator

The closest thing to a migration guide that exists today is the process for moving an
existing, hand-written mapping project onto the
[source generator](../06-Source-Generators/Mapping-Generator.md) — this is itself a form of
migration (from `NodeBuilder<TContext>`'s five reflective passes to compile-time generation),
and it's the one documented in detail:

1. Make every mapping class `partial`.
2. Make `BaseModelMappingRegistration<T>.Register()` `virtual`.
3. Expose the mapping constructor's alias/model-name as `protected` properties.
4. Reference the generator project as an `Analyzer`, not a normal project reference.
5. Drop the `NodeBuilder<TContext>.BuildFromMappings()` call from startup — registration now
   happens per-instance via each mapping class's generated `Register()` override.

See [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md#required-changes-to-existing-hand-written-code)
for the full, exact steps, including how ambiguous navigations are handled differently
(a build-time `CBMAP003` diagnostic instead of a runtime exception).

---

## Related Documentation

- [Changelog](Changelog.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Source Generators → Diagnostics](../06-Source-Generators/Diagnostics.md)

---

← Previous: [Changelog](Changelog.md)  |  Next: [Documentation Home](../README.md) →

---

[Home](../../README.md) → [Documentation](../README.md) → [Reference](README.md) → **Roadmap**

# Roadmap

> This roadmap predates the Phase 1 / future-phases framing introduced in
> [Architecture → Vision](../02-Architecture/Vision.md); the two describe the same trajectory
> at different levels of detail. Vision gives the four-item Phase 1 scope and four-item
> future-phases summary; this page gives the longer, phase-by-phase engineering breakdown.
> Where they'd conflict, Vision's Phase 1 boundary (EF Core mapping, Hot Chocolate,
> PostgreSQL, Dapper) is authoritative for "what's built today."

## Contents

- [Vision](#vision)
- [Phase 1 — Foundation](#phase-1--foundation)
- [Goals](#goals)
- [Phase 2 — Runtime](#phase-2--runtime)
- [Goals](#goals)
- [Phase 3 — SQL](#phase-3--sql)
- [Goals](#goals)
- [Phase 4 — Incremental Generator](#phase-4--incremental-generator)
- [Goals](#goals)
- [Phase 5 — Dependency Inversion](#phase-5--dependency-inversion)
- [Goals](#goals)
- [Phase 6 — GraphQL](#phase-6--graphql)
- [Goals](#goals)
- [Phase 7 — gRPC](#phase-7--grpc)
- [Goals](#goals)
- [Phase 8 — Web API](#phase-8--web-api)
- [Goals](#goals)
- [Phase 9 — Additional SQL Providers](#phase-9--additional-sql-providers)
- [Phase 10 — Graph Improvements](#phase-10--graph-improvements)
- [Phase 11 — Performance](#phase-11--performance)
- [Phase 12 — Native AOT](#phase-12--native-aot)
- [Phase 13 — Tooling](#phase-13--tooling)
- [Phase 14 — Ecosystem](#phase-14--ecosystem)
- [Success Criteria](#success-criteria)
- [Guiding Principles](#guiding-principles)
- [Summary](#summary)

---

> This document describes the long-term technical roadmap for the CoffeeBeanery framework. It outlines the expected evolution of the architecture while preserving the project's core design principles.

The roadmap is intentionally organized around architectural capabilities rather than release dates.

---

## Vision

CoffeeBeanery aims to become a compile-time-first data access framework capable of supporting multiple transports, multiple SQL dialects, and graph-based querying through a shared execution engine.

The long-term architecture is:

```
             Foundation
                  ▲
                  │
    ┌─────────────┼─────────────┐
    │             │             │
 Runtime         SQL      Source Generator
    ▲             ▲             │
    │             │             │
    └─────────────┼─────────────┘
                  │
       Generated Runtime Components
                  ▲
                  │
      ┌───────────┼───────────┐
      │           │           │
   GraphQL      gRPC       Web API
```

---

## Phase 1 — Foundation

## Goals

- Stable contracts
- Immutable metadata
- Planning primitives
- Runtime primitives
- Identifier system
- Dependency inversion

### Deliverables

- EntityMetadata
- ModelMetadata
- ColumnMetadata
- JoinMetadata
- GraphMetadata
- ColumnReference
- QueryPlan
- MutationPlan
- IMetadataProvider
- IPlannerRegistry

This phase establishes the architectural foundation.

---

## Phase 2 — Runtime

## Goals

- Query execution
- Mutation execution
- Materialization pipeline
- Transaction coordination
- Dependency graph execution

### Deliverables

- QueryExecutor
- MutationExecutor
- ExecutionContext
- Runtime services
- Execution pipeline

Runtime should depend only on Foundation.

---

## Phase 3 — SQL

## Goals

- PostgreSQL support
- SQL writers
- SQL readers
- SQL dialect abstraction
- Apache AGE integration

### Deliverables

- PostgresSqlWriter
- PostgresSqlReader
- PostgreSqlDialect
- SQL builders
- SQL visitors

SQL serializes execution plans without performing planning.

---

## Phase 4 — Incremental Generator

## Goals

- Complete compile-time analysis
- Metadata generation
- Materializer generation
- Planner generation
- Runtime registrations

### Deliverables

- MetadataEmitter
- PlannerEmitter
- MaterializerEmitter
- DematerializerEmitter
- DependencyInjectionEmitter

Generated code should contain only precomputed information.

---

## Phase 5 — Dependency Inversion

## Goals

Replace static generated classes with generated implementations of Foundation contracts.

Instead of:

```
GeneratedMetadata.GetEntity(...)
```

Runtime should consume:

```csharp
IMetadataProvider
```

implemented by:

```csharp
GeneratedMetadataProvider
```

This decouples Runtime from generated code.

---

## Phase 6 — GraphQL

## Goals

- Schema generation
- Resolver generation
- Middleware
- Dependency Injection
- Transport adapter

GraphQL becomes a thin layer over Runtime.

---

## Phase 7 — gRPC

## Goals

- Protobuf integration
- Service generation
- Runtime adapter

The same Runtime should execute GraphQL and gRPC requests.

---

## Phase 8 — Web API

## Goals

- Controllers
- Minimal APIs
- OpenAPI integration
- Runtime adapter

Execution remains identical to GraphQL and gRPC.

---

## Phase 9 — Additional SQL Providers

Potential providers include:

- SQL Server
- MySQL
- SQLite
- CockroachDB
- YugabyteDB

The planner remains unchanged.

Only SQL serialization changes.

---

## Phase 10 — Graph Improvements

Future graph capabilities include:

- Recursive traversals
- Variable-length paths
- Path projections
- Graph aggregation
- Graph mutation optimization
- Graph query caching

These features extend planning while preserving Runtime behavior.

---

## Phase 11 — Performance

Areas of ongoing optimization:

- Reduced allocations
- Faster metadata lookup
- Improved SQL generation
- Streaming materialization
- Better cache locality
- Batch execution
- Object pooling where appropriate

Performance improvements should remain architecture-driven.

---

## Phase 12 — Native AOT

Continue validating:

- Metadata provider
- Materializers
- Planner registry
- SQL generation
- Runtime execution

No feature should compromise Native AOT compatibility.

---

## Phase 13 — Tooling

Developer tooling may include:

- Visual Studio integration
- Roslyn analyzers
- Diagnostic visualizers
- SQL preview
- Query plan visualizer
- Graph explorer

These tools should consume generated metadata where possible.

---

## Phase 14 — Ecosystem

Potential ecosystem projects:

```
CoffeeBeanery.Mongo

CoffeeBeanery.Redis

CoffeeBeanery.Cosmos

CoffeeBeanery.Elasticsearch

CoffeeBeanery.Blazor

CoffeeBeanery.OpenApi

CoffeeBeanery.Cli
```

Each project integrates through Foundation contracts.

---

## Success Criteria

CoffeeBeanery will be considered architecturally complete when:

- Runtime contains no reflection.
- Generated code implements all required contracts.
- GraphQL, gRPC, and Web API share the same Runtime.
- SQL dialects are interchangeable.
- Native AOT is fully supported.
- Metadata is immutable.
- Execution plans are immutable.
- Generated artifacts are deterministic.

---

## Guiding Principles

Future development should preserve the following principles:

- Compile-time over runtime
- Immutable metadata
- Immutable execution plans
- Dependency inversion
- Transport independence
- Database abstraction
- Deterministic generation
- Clear project boundaries
- Single responsibility

Architectural consistency is more valuable than short-term convenience.

---

## Summary

The CoffeeBeanery roadmap is focused on evolving the framework through compile-time generation, transport independence, and dependency inversion.

Rather than adding isolated features, each phase strengthens the overall architecture, ensuring the framework remains performant, maintainable, extensible, and capable of supporting new transports and storage providers without compromising its core design.

---

## Related Documentation

- [Architecture → Vision](../02-Architecture/Vision.md)
- [Changelog](Changelog.md)
- [ADRs](ADRs.md)

---

← Previous: [Glossary](Glossary.md)  |  Next: [Changelog](Changelog.md) →
