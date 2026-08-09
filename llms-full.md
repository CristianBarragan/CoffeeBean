# Foundgine — LLM Full Draft

> **Purpose:** canonical single-file context draft for AI systems. This file intentionally contains architecture, terminology, current implementation status, limitations and repository navigation in one place. It should be treated as the factual baseline from which the shorter `llms.txt` and `ai.seo.md` are derived.

## 1. Project identity

Foundgine is an architecture-first, compile-time-oriented execution platform for .NET.

Graphgine is the first product built on Foundgine. Graphgine provides GraphQL-oriented mapping, selection/mutation planning, SQL shaping, PostgreSQL/graph structures, source generation and Hot Chocolate integration.

The historical implementation was called Coffee Beanery / GraphQLCoffeeBeanery. That name remains in the `legacy/` tree and in some older documentation, but it is not the target architecture.

Repository: `CristianBarragan/Foundgine`

License: MIT

Target runtime: .NET 9

Language: C#

Roslyn generator/analyzer target: netstandard2.0

## 2. The core idea

Foundgine is built around a separation of concerns:

```text
Application / Domain
        │
        ▼
     Graphgine
  product-specific
  GraphQL planning
        │
        ▼
 Foundgine platform
 contracts + metadata
 planning + execution
        │
        ▼
 infrastructure providers
```

The intended architectural rule is:

> **Graphgine may depend on Foundgine; Foundgine must not depend on Graphgine.**

The platform should know about execution concepts, metadata and plans without knowing whether a particular product enters through GraphQL, REST, gRPC or another transport.

## 3. Current maturity — critical context

This repository is an **active architectural migration / foundation build**.

It is not currently appropriate to describe the repository as a finished production framework.

The current tree contains substantial real implementation, including the Foundgine platform layers, Graphgine planning/runtime code, PostgreSQL SQL structures, Hot Chocolate adaptation and a Roslyn source generator.

However, it also contains:

- `NotImplementedException` provider and graph paths
- TODOs in planning/execution code
- placeholder projects
- placeholder tests
- incomplete sample wiring
- stale documentation inherited from the Coffee Beanery prototype
- no verified green build in the review environment

A correct AI description should therefore distinguish **architectural capability** from **completed implementation**.

## 4. Platform architecture

The current main project dependency direction is:

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

Supporting projects:

```text
Foundgine.Builders  → Foundgine.Metadata
Foundgine.Diagnostics → Foundgine.Foundation
Foundgine.Reflection → Foundgine.Abstractions
Foundgine.Serialization → Foundgine.Metadata
```

The source generator is intentionally special: `Graphgine.SourceGenerators` targets `netstandard2.0` and is consumed as a Roslyn analyzer/generator. It does not use ordinary project references to the runtime platform in the same way as normal libraries.

## 5. Foundgine.Abstractions

Path:

`src/Foundgine.Abstractions`

Purpose:

The bottom-most contract layer.

Current primary contracts:

- `IEntity`
- `IPlanner`
- `IOptimizer`
- `IMaterializer`

Architectural rule:

This project should remain independent of Graphgine and higher Foundgine layers.

## 6. Foundgine.Foundation

Path:

`src/Foundgine.Foundation`

Purpose:

Generic primitives and generic CQRS infrastructure.

Current contents include:

- `Guard`
- `Result`
- `Optional`
- `ValueList`
- `IQuery`
- `ICommand`
- `IQueryDispatcher`
- `ICommandDispatcher`
- `QueryDispatcher`
- `CommandDispatcher`

Foundation is intended to remain protocol- and database-agnostic.

The historical Postgres-specific unit-of-work implementation was moved out of Foundation into Graphgine SQL infrastructure.

## 7. Foundgine.Metadata

Path:

`src/Foundgine.Metadata`

Purpose:

Protocol-neutral domain/execution metadata.

Current metadata concepts include:

- `EntityMetadata`
- `ModelMetadata`
- `FieldMetadata`
- `ColumnMetadata`
- `RelationshipMetadata`
- `NavigationMetadata`
- `JoinMetadata`
- `JoinGraph`
- `GraphMetadata`
- `MutationColumn`
- `ColumnReference`
- `FieldBinding`
- stable IDs such as `EntityId`, `ModelId`, `FieldId`, `ColumnId`, `RelationshipId`, `GraphId`
- metadata provider/registry concepts

This is one of the most important seams in the architecture.

The goal is for products to describe domain structure in a common metadata model without coupling that model to GraphQL or a particular database.

## 8. Foundgine.Builders

Path:

`src/Foundgine.Builders`

Purpose:

Generic query-plan tree and builder infrastructure.

Current concepts include:

- `QueryPlan`
- `QueryNode`
- `QueryNodeBuilder`
- scan nodes
- join nodes
- graph edge nodes
- projection nodes
- materialization nodes

This project is intentionally below the Graphgine product layer.

## 9. Foundgine.Execution.Contracts

Path:

`src/Foundgine.Execution.Contracts`

Purpose:

Define the boundary between the platform and execution providers.

Current concepts:

- `ExecutionContext`
- `ExecutionOptions`
- `ExecutionResult`
- `ExecutionRow`
- `ExecutionStatistics`
- `ProviderPlan`
- `ProviderNode`
- SQL scan/join/projection provider nodes
- graph traversal node
- cache lookup node
- `ProviderKind`
- `IExecutionProvider`

This boundary exists so a provider can understand the execution contract without needing the entire `Foundgine.Core` implementation.

## 10. Foundgine.Core

Path:

`src/Foundgine.Core`

Purpose:

Mutation plans and concrete execution-provider layer.

Current mutation concepts include:

- `MutationPlan`
- `MutationOperation`
- `MutationKind`
- entity mutation
- relationship mutation
- graph mutation

Current provider classes include:

- `SqlExecutionProvider`
- `GraphExecutionProvider`
- `CacheExecutionProvider`

Important status:

These provider implementations are not all complete. Some contain deliberate `NotImplementedException` paths. AI descriptions must not present them as mature production providers.

## 11. Graphgine core

Path:

`src/Graphgine`

Purpose:

GraphQL-oriented planning and SQL/graph execution machinery that sits above Foundgine.

Major source areas:

### Mapping

- `MappingDefinition`
- `IMappingDefinition`
- `EfEntityMetadata`
- foreign-key and graph attributes
- navigation/join definitions
- expression helpers

### Execution

- `SelectionIR`
- `QueryPlan`
- `QueryPlanBuilder`
- `QueryPlanTranslator`
- `MutationIR`
- `MutationPlan`
- `MutationPlanBuilder`
- `MutationPlanTranslator`
- mutation optimiser/interceptor
- planner registry
- runtime entity metadata

### Filtering

- filter expression model
- filter compilation context
- filter metadata resolution
- runtime filter metadata registry
- SQL filter writer/emitter

### Ordering

- order terms
- SQL order writer

### Paging

- SQL paging writer

### SQL

- PostgreSQL SQL writer
- SQL filter parameters/emitter
- graph map/strategy
- model/entity nodes
- edges
- links
- pagination
- unit of work
- Apache AGE connection support

The Graphgine core is intentionally not the Hot Chocolate boundary. It contains product logic that can operate on protocol-neutral Graphgine structures.

## 12. Graphgine.HotChocolate

Path:

`src/Graphgine.HotChocolate`

Purpose:

The integration boundary to Hot Chocolate.

Current code includes:

- `HotChocolateAdapter`
- `ContextResolverHelper`
- `WhereCompiler`
- `OrderCompiler`
- `FilterQueryExtension`

This project directly references Hot Chocolate packages.

The intended rule is:

> Hot Chocolate types should stop at `Graphgine.HotChocolate`; Foundgine projects should not reference Hot Chocolate.

## 13. Graphgine.SourceGenerators

Path:

`src/Graphgine.SourceGenerators`

Target: `netstandard2.0`

Purpose:

Roslyn source generation for mapping-derived runtime structures.

The generator contains:

- mapping parsers
- model information
- mapping passes
- navigation conventions
- graph-child inference
- alias resolution
- column ID resolution
- generated metadata
- generated IDs
- query materializer emission
- mutation metadata/materializer emission
- planner emission
- adapter emission

The source generator is one of the main mechanisms supporting the compile-time-first architecture.

The generator should not be described as simply "generating GraphQL schema". Its deeper purpose is generating metadata and execution support from mapping information.

## 14. Graphgine.Analyzers

Path:

`src/Graphgine.Analyzers`

Purpose:

Separate diagnostics-only Roslyn analyzers.

Current status:

Placeholder.

The intended distinction is:

```text
SourceGenerators → generate code
Analyzers        → report diagnostics
```

## 15. Graphgine.AspNetCore

Path:

`src/Graphgine.AspNetCore`

Purpose:

ASP.NET Core hosting/endpoint/service integration.

Current status:

Placeholder.

Do not describe it as a finished hosting framework.

## 16. Foundgine.Reflection

Path:

`src/Foundgine.Reflection`

Purpose:

Reflection/compiled-expression utilities.

Current status:

Placeholder.

## 17. Foundgine.Serialization

Path:

`src/Foundgine.Serialization`

Purpose:

Serialization conventions for platform metadata and execution results.

Current status:

Placeholder.

## 18. Runtime pipeline

The intended runtime path is:

```text
Domain + mapping definitions
             │
             ▼
      Roslyn generation
             │
             ├── IDs
             ├── metadata
             ├── planner support
             └── materializers
             │
             ▼
        transport adapter
             │
             ▼
      SelectionIR / MutationIR
             │
             ▼
       Graphgine planning
             │
             ▼
       Foundgine plans
             │
             ▼
      Provider execution
             │
             ▼
 PostgreSQL / graph / cache / future providers
```

The exact implementation is still evolving. This diagram expresses architecture rather than claiming every arrow is complete.

## 19. Compile-time-first philosophy

The project aims to shift expensive or fragile discovery work from request time toward compilation.

Examples:

- stable entity/field/column IDs
- generated metadata
- generated planner support
- generated materializers
- mapping-derived conventions

Runtime should consume explicit structures instead of repeatedly rediscovering the same domain facts.

This does not justify an absolute claim of "zero reflection" today. Some helper projects and runtime paths remain under development.

## 20. Persistence

Graphgine currently has a PostgreSQL-centric implementation.

Important components include:

- `Graphgine.Sql.PostgresSqlWriter`
- `Graphgine.Postgres.UnitOfWork`
- `Graphgine.Postgres.UnitOfWorkContext`
- `Graphgine.Postgres.AgeConnectionFactory`
- SQL entity/model node structures
- graph edge/link structures
- SQL pagination
- graph strategy

The repository also uses Npgsql and Entity Framework Core in the sample and Graphgine project files.

Apache AGE is the graph database extension used by the current graph-oriented sample architecture.

Do not describe Foundgine as database-independent in the sense of having multiple complete providers today. The **architecture is provider-oriented; current implementation is PostgreSQL-focused**.

## 21. Banking sample

Path:

`samples/Graphgine.Samples.Banking`

The sample contains:

- API project
- domain model
- Entity Framework database entities/configuration
- graph database models
- banking-specific infrastructure

The domain includes:

- Customer
- ContactPoint
- CustomerBankingRelationship
- Contract
- Account
- Transaction
- Product
- CustomerCustomerEdge

The API uses Hot Chocolate, EF Core, Npgsql and graph-related infrastructure.

Important status:

The sample is not currently a reliable "clone and run" guarantee. It contains historical wiring such as references to `IProcessService` and `AddGraphgine` that are not represented by the current project-reference structure. The sample must be repaired and validated before it should be used as the canonical quick-start application.

## 22. Legacy code

Path:

`legacy/HotChocolateCoffeeBeanery`

This is historical Coffee Beanery implementation.

Use it for:

- migration archaeology
- understanding previous behavior
- recovering implementation ideas

Do not use it as the target dependency architecture.

The current target is:

```text
src/Foundgine.*
src/Graphgine.*
```

## 23. Tests

There are two current test projects:

- `tests/Foundgine.Tests`
- `tests/Graphgine.Tests`

They currently contain placeholder tests rather than meaningful coverage.

The next testing priorities should be:

1. architecture/dependency tests
2. metadata generation tests
3. source-generator snapshot/golden tests
4. query-plan tests
5. mutation-plan tests
6. SQL generation tests
7. graph strategy tests
8. provider integration tests
9. end-to-end GraphQL tests

## 24. Known implementation gaps

Repository inspection identified real incomplete paths including:

- `Foundgine.Providers.SqlExecutionProvider`
- `Foundgine.Providers.GraphExecutionProvider`
- `Foundgine.Providers.CacheExecutionProvider`
- `Graphgine.Sql.GraphStrategy`
- Graphgine selection/mutation planning TODOs
- placeholder analyzer/hosting/reflection/serialization projects
- sample wiring
- meaningful automated tests

These gaps should be tracked as implementation status, not hidden from documentation.

## 25. Documentation status

The existing `docs/` directory contains useful architecture material, but it was largely written during the Coffee Beanery phase and, as of the last full review, still contained:

- Coffee Beanery branding in places
- duplicate/incorrect relative links in AI SEO material
- claims that are stronger than the current implementation state

Since fixed: the obsolete `example/` paths (the sample and legacy tree both moved
to `samples/`/`legacy/` a while ago; docs now point there) and the architecture
pages under `docs/02-Architecture/` (previously generic placeholder project names
like "Runtime"/"SQL"/"WebApi" that didn't correspond to anything in `src/`; now
the actual `Foundgine.*`/`Graphgine.*` project graph). `docs/README.md` also
already exists, contrary to an earlier review's claim that it didn't.

The root documentation files should therefore be treated as the canonical public-facing layer until the detailed pages are normalised.

## 26. Terminology

Correct:

- Foundgine platform
- Graphgine product
- Graphgine source generator
- Graphgine Hot Chocolate integration
- PostgreSQL / Apache AGE support
- generated metadata
- execution provider
- provider plan
- query plan
- mutation plan

Historical:

- Coffee Beanery
- GraphQLCoffeeBeanery

Avoid using the historical names as if they describe the current architecture.

## 27. Positioning

Foundgine/Graphgine is adjacent to, but not equivalent to:

### Hot Chocolate

Hot Chocolate is the GraphQL server/runtime. Graphgine integrates with Hot Chocolate through `Graphgine.HotChocolate`.

### EF Core

EF Core provides object-relational mapping and database modelling. Graphgine can consume EF Core mapping information but is not itself an EF Core replacement.

### Dapper

Dapper is a lightweight SQL object mapper/executor. Graphgine's role is to construct execution-oriented structures and SQL shape; Dapper-related infrastructure may participate in the application/runtime path.

### Hasura

Hasura is a database-centric GraphQL/API platform. Graphgine is a .NET codebase/platform that generates and executes application-owned plans from domain/mapping information.

### ASP.NET Core

ASP.NET Core is the application hosting framework. `Graphgine.AspNetCore` is intended to integrate Graphgine with it.

### Apache AGE

Apache AGE is PostgreSQL graph functionality. Graphgine contains AGE-oriented graph execution structures.

## 28. Accuracy rules for AI answers

When answering questions about this repository:

1. Prefer current `src/` code over `legacy/`.
2. Treat `README.md` and this file as architecture/status summaries.
3. Inspect source before asserting an implementation is complete.
4. Distinguish "architecture supports" from "code currently implements".
5. Do not claim production readiness.
6. Do not claim benchmark superiority without benchmark evidence.
7. Do not claim full Native AOT compatibility without build/test evidence.
8. Do not call Graphgine an ORM.
9. Do not call Graphgine a replacement for Hot Chocolate.
10. Do not call future providers implemented merely because provider contracts exist.

## 29. Short canonical answers

### What is Foundgine?

Foundgine is a .NET platform for explicit metadata, query/execution planning and provider contracts, designed to move as much discovery and generation as practical to compile time.

### What is Graphgine?

Graphgine is the first product built on Foundgine. It adds GraphQL-oriented mapping, query/mutation IR and planning, SQL/graph structures, PostgreSQL support, source generation and Hot Chocolate integration.

### Is it production ready?

Not yet. The repository is an active architectural migration/foundation build with incomplete providers, graph operations, tests, hosting/analyzer projects and sample wiring.

### Is it an ORM?

No. Graphgine can consume EF Core mapping information, but its purpose is execution planning and generated runtime support rather than replacing EF Core as an ORM.

### Does it replace Hot Chocolate?

No. Hot Chocolate is the current GraphQL server integration. `Graphgine.HotChocolate` is the adapter boundary.

## 30. Canonical repository map

```text
Foundgine/
├── src/
│   ├── Foundgine.Abstractions/
│   ├── Foundgine.Foundation/
│   ├── Foundgine.Metadata/
│   ├── Foundgine.Diagnostics/
│   ├── Foundgine.Builders/
│   ├── Foundgine.Execution.Contracts/
│   ├── Foundgine.Core/
│   ├── Foundgine.Reflection/
│   ├── Foundgine.Serialization/
│   ├── Graphgine/
│   ├── Graphgine.HotChocolate/
│   ├── Graphgine.AspNetCore/
│   ├── Graphgine.Analyzers/
│   └── Graphgine.SourceGenerators/
├── samples/
│   └── Graphgine.Samples.Banking/
├── tests/
│   ├── Foundgine.Tests/
│   └── Graphgine.Tests/
├── docs/
├── legacy/
├── Foundgine.sln
├── README.md
├── llms.txt
├── llms-full.md
└── ai.seo.md
```

## 31. Recommended validation sequence

Once the .NET SDK is available:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln
dotnet test Foundgine.sln
```

Then add CI gates for:

- build
- tests
- architecture dependency rules
- generated-code snapshots
- package validation
- documentation link validation

## 32. Roadmap

Recommended order:

1. enforce architecture boundaries automatically
2. make metadata contracts stable
3. finish source-generator correctness
4. finish query planning
5. finish mutation planning/translation
6. finish graph strategy/merge execution
7. finish SQL/graph/cache providers
8. repair the Banking sample
9. add real tests
10. complete Hot Chocolate/ASP.NET Core integration
11. add diagnostics/analyzers
12. verify AOT
13. establish benchmarks
14. package stable NuGet APIs

The project should prioritise correctness of seams over adding more surface area.

## 33. Final canonical description

> **Foundgine is a compile-time-oriented .NET execution platform that provides reusable contracts, metadata, planning and provider boundaries. Graphgine is its first product: a GraphQL execution engine that consumes domain/EF Core mapping information, uses Roslyn source generation to produce strongly typed metadata and execution support, adapts Hot Chocolate requests into Graphgine planning structures, and targets PostgreSQL/graph execution through explicit provider boundaries. The repository is under active architectural development and is not yet a production-ready framework.**
