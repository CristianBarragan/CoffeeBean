# Foundgine

**Foundgine is a compile-time-first execution platform for .NET. Graphgine is its first product: a GraphQL execution engine built on the Foundgine platform.**

Foundgine is under construction, refer to legacy folder containing former GraphQL-CoffeeBeanery repo. Once Foundfine and Graphgine are stable, GraphQL-CoffeeBeanery will be removed

Foundgine is being shaped around a strict separation between:

- **platform contracts and primitives** — `Foundgine.*`
- **product-specific execution and GraphQL behavior** — `Graphgine.*`
- **application/domain code** — `samples/*`
- **historical code** — `legacy/*`

The central idea is simple:

> **Describe the domain and its relationships once, generate the executable metadata and planning structures at build time, then execute through explicit providers at runtime.**

This repository is currently an **active architectural migration / foundation build**, not a finished production framework. The source tree contains substantial real implementation, but several provider paths, graph operations, analyzers, hosting integration, tests, and packaging surfaces are still incomplete.

## What Foundgine is

Foundgine provides the lower-level pieces needed by products that want deterministic, generated execution:

```text
Application / Domain
        │
        ▼
     Graphgine
  GraphQL + mapping
  planning + SQL shape
        │
        ▼
 Foundgine.Core
 mutation plans
 provider implementations
        │
        ▼
Foundgine.Execution.Contracts
 execution context
 provider plan
 execution result
        │
        ▼
Foundgine.Metadata / Builders
        │
        ▼
Foundgine.Foundation
        │
        ▼
Foundgine.Abstractions
```

The intended dependency direction is **inward/downward only**. Foundgine does not depend on Graphgine.

### Foundgine vs. Graphgine

**Foundgine** is the reusable platform:

- contracts
- metadata
- query-plan structures
- execution contracts
- mutation-plan structures
- diagnostics
- foundational primitives
- reflection/serialization extension points

**Graphgine** is the first product built on that platform:

- GraphQL-oriented mapping
- selection/query/mutation IR
- query and mutation planning
- filtering
- ordering
- pagination
- SQL shaping
- PostgreSQL / Apache AGE integration
- Hot Chocolate adaptation
- Roslyn source generation

A future product should be able to reuse Foundgine without taking a GraphQL dependency.

## Current status

### Implemented architectural foundation

The repository currently contains real code for:

- `Foundgine.Abstractions`
- `Foundgine.Foundation`
- `Foundgine.Metadata`
- `Foundgine.Diagnostics`
- `Foundgine.Builders`
- `Foundgine.Execution.Contracts`
- `Foundgine.Core`
- `Graphgine`
- `Graphgine.HotChocolate`
- `Graphgine.SourceGenerators`

The repository also contains:

- a Banking sample under `samples/Graphgine.Samples.Banking`
- placeholder test projects
- legacy Coffee Beanery code under `legacy/`
- extensive architecture/design documentation under `docs/`

### Important limitations

Do **not** interpret the repository as production-ready yet.

The current tree still contains:

- `NotImplementedException` provider/graph paths
- TODOs in execution/planning code
- placeholder projects for `Foundgine.Reflection`, `Foundgine.Serialization`, `Graphgine.AspNetCore`, and `Graphgine.Analyzers`
- placeholder unit tests
- incomplete end-to-end sample wiring
- stale historical documentation inherited from the Coffee Beanery prototype
- no verified green build in this review environment

The source tree is therefore best described as an **architecture-first framework under active construction**.

## Projects

### Foundgine platform

| Project | Responsibility |
|---|---|
| `Foundgine.Abstractions` | Lowest-level contracts: `IEntity`, `IPlanner`, `IOptimizer`, `IMaterializer`. |
| `Foundgine.Foundation` | Generic primitives such as `Guard`, `Result`, `Optional`, `ValueList`, plus generic CQRS contracts/dispatchers. |
| `Foundgine.Metadata` | Protocol-neutral entity, field, column, relationship, graph, join and mutation metadata. |
| `Foundgine.Diagnostics` | Shared diagnostic events, scopes and listeners. |
| `Foundgine.Builders` | Generic query-plan tree and builder types such as `QueryPlan`, `QueryNode`, and `QueryNodeBuilder`. |
| `Foundgine.Execution.Contracts` | Runtime execution boundary: `ExecutionContext`, `ExecutionOptions`, `ExecutionResult`, `ProviderPlan`, provider nodes and `IExecutionProvider`. |
| `Foundgine.Core` | Mutation plans and concrete execution-provider implementations/skeletons. |
| `Foundgine.Reflection` | Reserved extension point for reflection/compiled-expression helpers; currently placeholder. |
| `Foundgine.Serialization` | Reserved serialization layer; currently placeholder. |

### Graphgine product

| Project | Responsibility |
|---|---|
| `Graphgine` | GraphQL-neutral planning and SQL/graph execution machinery. |
| `Graphgine.HotChocolate` | Hot Chocolate adapter. This is the only Graphgine project intended to reference Hot Chocolate directly. |
| `Graphgine.AspNetCore` | ASP.NET Core hosting integration; currently placeholder. |
| `Graphgine.Analyzers` | Diagnostics-only Roslyn analyzers; currently placeholder. |
| `Graphgine.SourceGenerators` | Roslyn source generator for mapping-derived IDs, metadata, planners and materializers. |

## Dependency boundaries

The most important architectural rule is:

```text
Graphgine.*  →  Foundgine.*
Foundgine.*  →  lower Foundgine.* layers
Foundgine.*  ✕  Graphgine.*
```

The current project references establish the main chain:

```text
Graphgine.HotChocolate
        ↓
    Graphgine
        ↓
  Foundgine.Core
        ↓
Foundgine.Execution.Contracts
        ↓
Foundgine.Metadata
        ↓
Foundgine.Foundation
        ↓
Foundgine.Abstractions
```

`Foundgine.Builders` and `Foundgine.Diagnostics` branch from the lower platform layers, while `Graphgine.SourceGenerators` is intentionally separate because a Roslyn generator must run as an analyzer rather than reference the consuming runtime assemblies in the normal way.

## Compile-time model

The intended execution model is:

```text
Domain / EF Core mapping
          │
          ▼
   Roslyn source generator
          │
          ├── IDs
          ├── metadata
          ├── planner information
          ├── materializers
          └── adapter support
          │
          ▼
   Graphgine planning/runtime
          │
          ▼
   Foundgine execution contracts
          │
          ▼
       Provider
          │
          ▼
     infrastructure
```

The important distinction is that **metadata discovery and code generation happen during compilation wherever practical**, while runtime code consumes explicit metadata and plans.

This is a design goal, not a claim that every current path is completely reflection-free or AOT-clean.

## Graphgine today

The current Graphgine codebase contains several substantial subsystems:

- mapping definitions and EF Core metadata
- selection IR
- query-plan building and translation
- mutation IR and mutation planning
- mutation optimization/interception
- filter expression compilation
- SQL filter emission
- ordering SQL generation
- paging SQL generation
- PostgreSQL SQL writing
- graph structures and graph strategy
- Apache AGE connection support
- Hot Chocolate request adaptation
- Hot Chocolate Relay-style connection result construction
- Roslyn mapping/source generation

The source generator contains dedicated parsing, inference, convention, alias-resolution and emission passes. It emits strongly typed identifiers and generated execution-related code rather than requiring all mapping information to be rediscovered at runtime.

## Persistence

The current Graphgine persistence layer is strongly PostgreSQL-oriented.

Relevant code includes:

- `Graphgine.Sql.PostgresSqlWriter`
- `Graphgine.Sql.UnitOfWork`
- `Graphgine.Sql.UnitOfWorkContext`
- `Graphgine.Sql.AgeConnectionFactory`
- SQL graph nodes/edges and pagination structures
- PostgreSQL/AGE-specific graph handling

The Banking sample also uses:

- Entity Framework Core
- Npgsql
- Dapper-related packages
- Apache AGE integration

Provider abstraction exists at the Foundgine level, but **SQL/graph/cache provider completeness is not yet equivalent to a mature multi-provider framework**.

## Source generation

`Graphgine.SourceGenerators` targets `netstandard2.0` and uses Roslyn.

Its responsibilities include:

- parsing mapping classes
- resolving entity/model relationships
- inferring graph children and navigation conventions
- resolving columns and aliases
- emitting IDs
- emitting metadata
- emitting query materializers
- emitting mutation metadata/materializers
- emitting planner/adapter support

`Graphgine.Analyzers` is a separate intended project for diagnostics. It should not be confused with the source generator.

## Sample

The repository contains `samples/Graphgine.Samples.Banking`, which models a banking domain with entities such as:

- Customer
- ContactPoint
- CustomerBankingRelationship
- Contract
- Account
- Transaction
- Product
- CustomerCustomerEdge

The sample demonstrates the intended combination of:

```text
Domain model
   +
EF Core entity mappings
   +
Graphgine source generation
   +
Hot Chocolate
   +
PostgreSQL / Npgsql
   +
graph/AGE support
```

However, the sample currently contains historical wiring and references to services that are not present in the new platform project graph. It should therefore be treated as a migration/example fixture until its end-to-end build is repaired.

## Legacy code

`legacy/HotChocolateCoffeeBeanery` is historical source from the original Coffee Beanery implementation.

It is **not the architectural source of truth** for Foundgine.

The current `src/Foundgine.*` and `src/Graphgine.*` projects are the intended future structure. Legacy code is retained for migration/reference purposes.

## What Foundgine is not

Foundgine should not currently be described as:

- a completed ORM
- a complete GraphQL server
- a replacement for Hot Chocolate
- a database engine
- a workflow engine
- a general-purpose distributed runtime
- a production-ready multi-provider abstraction
- a fully implemented Native AOT framework

Graphgine is specifically the first GraphQL-oriented product on top of the platform.

## Documentation

The detailed architecture documentation is organised under:

- [Getting Started](docs/01-Getting-Started/README.md)
- [Architecture](docs/02-Architecture/README.md)
- [Foundation](docs/03-Foundation/README.md)
- [Runtime](docs/04-Runtime/README.md)
- [GraphQL](docs/05-GraphQL/README.md)
- [Source Generators](docs/06-Source-Generators/README.md)
- [Dependency Injection](docs/07-Dependency-Injection/README.md)
- [Persistence](docs/08-Persistence/README.md)
- [AI / LLM Readiness](docs/09-AI/README.md)
- [Performance](docs/10-Performance/README.md)
- [Samples](docs/11-Samples/README.md)
- [Contributing](docs/12-Contributing/README.md)
- [Reference](docs/13-Reference/README.md)

Some of those pages still contain Coffee Beanery-era wording and should be normalised against this document.

## Development

The solution is `Foundgine.sln`.

The repository targets .NET 9 for runtime libraries and .NET Standard 2.0 for the Roslyn generator/analyzer components.

A clean environment for this review did not contain the `dotnet` CLI, so this documentation deliberately makes **no claim of a verified build**.

Once the SDK is available, the first validation should be:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln
dotnet test Foundgine.sln
```

Those commands should become part of CI before the project is marketed as production-ready.

## Roadmap direction

The architectural direction is:

1. Stabilise the Foundgine dependency boundaries.
2. Make metadata and generated contracts authoritative.
3. Finish Graphgine query planning and execution.
4. Finish mutation translation and graph/merge execution.
5. Complete provider implementations.
6. Repair and simplify the Banking sample.
7. Add real architecture and behavioural tests.
8. Complete ASP.NET Core integration.
9. Add diagnostics/analyzers.
10. Verify AOT and performance claims with repeatable benchmarks.
11. Package and publish stable NuGet surfaces.

The goal is not to add features before the seams are reliable.

## License

MIT. See [LICENSE](LICENSE).
