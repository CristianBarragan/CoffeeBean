> This file is an alias of llms.txt for tools that specifically look for AI.SEO.md at the repository root — see docs/09-AI/LLM-Readiness.md.

# Coffee Beanery

> Coffee Beanery is a compile-time execution engine that transforms business models into deterministic execution plans, independent of transport, database, or infrastructure. Everything else is an adapter. Phase 1: EF Core mapping as the metadata source, Hot Chocolate as the transport, PostgreSQL as the execution provider, Dapper as the SQL executor.

Full documentation lives at docs/README.md; a single-file concatenation for tools that prefer one ingest target is at llms-full.md. See docs/09-AI/LLM-Readiness.md for details on this file.

## Documentation

- [Getting Started](docs/01-Getting-Started/README.md): This section gets a Coffee Beanery-backed service running locally and explains the moving
  - [Configuration](docs/01-Getting-Started/Configuration.md): - [Connection strings](#connection-strings)
  - [Getting Started FAQ](docs/01-Getting-Started/FAQ.md): - [Do I need to learn a new modeling API?](#do-i-need-to-learn-a-new-modeling-api)
  - [Your First Service](docs/01-Getting-Started/First-Service.md): - [What you're running](#what-youre-running)
  - [Installation](docs/01-Getting-Started/Installation.md): - [Prerequisites](#prerequisites)
- [Architecture](docs/02-Architecture/README.md): Coffee Beanery is a compile-time execution engine. This section explains the vision, the
  - [Dependency Graph](docs/02-Architecture/Dependency-Graph.md): - [Dependency Graph](#dependency-graph-1)
  - [Layers](docs/02-Architecture/Layers.md): - [Design Goals](#design-goals)
  - [Principles](docs/02-Architecture/Principles.md): - [The Five Core Principles](#the-five-core-principles)
  - [Request Pipeline](docs/02-Architecture/Request-Pipeline.md): - [Overview](#overview)
  - [Vision](docs/02-Architecture/Vision.md): - [The bold statement](#the-bold-statement)
- [Foundation](docs/03-Foundation/README.md): Foundation is the dependency-free contract layer everything else in Coffee Beanery builds
  - [Components](docs/03-Foundation/Components.md): - [Responsibilities](#responsibilities)
  - [Contracts](docs/03-Foundation/Contracts.md): - [Interfaces](#interfaces)
  - [Extensibility](docs/03-Foundation/Extensibility.md): - [Philosophy](#philosophy)
  - [Metadata](docs/03-Foundation/Metadata.md): - [Metadata](#metadata-1)
- [Runtime](docs/04-Runtime/README.md): Runtime is where generated execution plans actually run. It never discovers metadata, parses
  - [Events](docs/04-Runtime/Events.md): - [Current State](#current-state)
  - [Execution](docs/04-Runtime/Execution.md): - [Runtime Pipeline](#runtime-pipeline)
  - [Mutations](docs/04-Runtime/Mutations.md): - [Overview](#overview)
  - [Queries](docs/04-Runtime/Queries.md): - [Philosophy](#philosophy)
- [GraphQL](docs/05-GraphQL/README.md): - [Schema](Schema.md) — how the schema is composed from generated node metadata
  - [Pagination, Filtering & Sorting](docs/05-GraphQL/Pagination-Filtering-Sorting.md): - [Where it's implemented](#where-its-implemented)
  - [Resolvers](docs/05-GraphQL/Resolvers.md): - [The wrapper pattern](#the-wrapper-pattern)
  - [Schema](docs/05-GraphQL/Schema.md): - [Where the schema comes from](#where-the-schema-comes-from)
- [Source Generators](docs/06-Source-Generators/README.md): The Roslyn incremental source generator is what makes Coffee Beanery's "compile-time first"
  - [Diagnostics](docs/06-Source-Generators/Diagnostics.md): - [Diagnostic Codes](#diagnostic-codes)
  - [Mapping Generator](docs/06-Source-Generators/Mapping-Generator.md): `CoffeeBeanery.GraphQL.Core.Mapping.Generators` is the concrete generator shipped in Phase 1.
  - [Pipeline Stages](docs/06-Source-Generators/Pipeline-Stages.md): - [Overview](#overview)
- [Dependency Injection](docs/07-Dependency-Injection/README.md): - [Registration](Registration.md) — the composition root and per-layer registration
  - [Lifetimes](docs/07-Dependency-Injection/Lifetimes.md): - [Lifetime Guidelines](#lifetime-guidelines)
  - [Registration](docs/07-Dependency-Injection/Registration.md): - [Composition Root](#composition-root)
- [Persistence](docs/08-Persistence/README.md): Persistence is where a generated execution plan meets an actual database. Phase 1 ships one
  - [Caching](docs/08-Persistence/Caching.md): - [Startup warmup](#startup-warmup)
  - [Dapper & EF Core](docs/08-Persistence/Dapper-EFCore.md): - [Two different jobs](#two-different-jobs)
  - [PostgreSQL & AGE](docs/08-Persistence/PostgreSQL-AGE.md): - [Why PostgreSQL is Phase 1](#why-postgresql-is-phase-1)
- [AI & LLM Readiness](docs/09-AI/README.md): - [Scope note](#scope-note)
  - [LLM Readiness](docs/09-AI/LLM-Readiness.md): - [What llms.txt is](#what-llmstxt-is)
- [Performance](docs/10-Performance/README.md): - [Native AOT](Native-AOT.md) — the design that makes AOT compatibility possible
  - [Benchmarks](docs/10-Performance/Benchmarks.md): - [Overview](#overview)
  - [Native AOT](docs/10-Performance/Native-AOT.md): - [Why Native AOT?](#why-native-aot)
- [Samples](docs/11-Samples/README.md): - [The Banking sample](#the-banking-sample)
- [Contributing](docs/12-Contributing/README.md): - [Code Style](Code-Style.md)
  - [ADR Process](docs/12-Contributing/ADR-Process.md): - [When to write an ADR](#when-to-write-an-adr)
  - [Code Style](docs/12-Contributing/Code-Style.md): - [General Principles](#general-principles)
  - [Testing](docs/12-Contributing/Testing.md): - [Philosophy](#philosophy)
- [Reference](docs/13-Reference/README.md): - [ADRs](ADRs.md) — Architecture Decision Records
  - [Architecture Decision Records](docs/13-Reference/ADRs.md): Twelve foundational ADRs, recorded when the compile-time-first architecture was adopted.
  - [Changelog](docs/13-Reference/Changelog.md): - [Unreleased](#unreleased)
  - [FAQ](docs/13-Reference/FAQ.md): Extended architecture FAQ. For first-hour setup questions, see
  - [Glossary](docs/13-Reference/Glossary.md): - [Entity](#entity)
  - [Migration Guides](docs/13-Reference/Migration-Guides.md): - [No versioned releases yet](#no-versioned-releases-yet)
  - [Roadmap](docs/13-Reference/Roadmap.md): - [Vision](#vision)

## Project

- [Project README](README.md): Quick start, feature highlights, architecture diagram
- [License (MIT)](LICENSE)
