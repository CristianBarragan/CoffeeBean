# Foundgine

Foundgine is an extensible application-framework and infrastructure platform for
.NET. **Graphgine** is the GraphQL engine built on top of it — the first product
on the platform, formerly known as GraphQLCoffeeBeanery.

```
Applications
     ▲
     │
  Graphgine
     ▲
     │
┌─────────────────────┐
│      Foundgine       │
│                       │
│ Abstractions          │
│ Foundation            │
│ Metadata              │
│ Diagnostics           │
│ Builders              │
│ Core                  │
│ Reflection            │
│ Serialization         │
└─────────────────────┘
     ▲
     │
   .NET
```

## Dependency direction

Strict rule, enforced by the `ProjectReference`s in every `.csproj` (verified —
see "Architecture fixes" below):

```
Graphgine
    │
    ▼
Foundgine.Core
    ▼
Foundgine.Execution.Contracts   (ExecutionContext, ProviderPlan, IExecutionProvider)
    ▼
Foundgine.Builders / Foundgine.Metadata
    ▼
Foundgine.Foundation
    ▼
Foundgine.Abstractions
```

Foundgine never references Graphgine — not in any `.csproj`, and not in any
`using` inside a `.cs` file either. Graphgine is simply a consumer of the
platform, the same way a second product would be.

### Architecture fixes applied to the migrated code

The mechanical namespace rewrite from the CoffeeBeanery prototype carried over
a few dependency-direction violations that the `ProjectReference`s alone
didn't catch (mostly stray `using`s and types that were in the wrong project
to begin with). These have been fixed:

- **`IExecutionProvider` no longer lives in `Foundgine.Abstractions`.** It
  needs `ProviderPlan`/`ExecutionContext`, which pulled `Foundgine.Core` in as
  a dependency of the bottom-most project — exactly backwards. Those types
  (`ExecutionContext`, `ExecutionOptions`, `ExecutionResult`,
  `ExecutionStatistics`, `ExecutionRow`, `ProviderPlan`, `ProviderNode`,
  `IExecutionProvider`) now live together in a new project,
  **`Foundgine.Execution.Contracts`**, which depends only on
  `Foundgine.Metadata`. `Foundgine.Core` depends on it (for its concrete
  `SqlExecutionProvider`/`GraphExecutionProvider`/`CacheExecutionProvider`),
  not the other way around. `Foundgine.Abstractions` is back to zero
  dependencies — just `IEntity`, `IPlanner`, `IOptimizer`, `IMaterializer`.
- **`Foundgine.Foundation` no longer references `Graphgine`.** `CQRS/IQuery.cs`
  had a stray, unused `using Graphgine.Execution;` left over from the rename.
- **`Foundgine.Foundation` no longer depends on Npgsql.** `UnitOfWork` and
  `UnitOfWorkContext` were Postgres-specific (`NpgsqlConnection`,
  `NpgsqlTransaction`) but lived in the platform's foundation layer, which is
  supposed to be usable by a Postgres, SQL Server, Mongo, or non-database
  product alike. Both moved to `Graphgine.Sql`, next to the repo's other
  Postgres-specific code (`AgeConnectionFactory`, `PostgresSqlWriter`).
  `Foundgine.Foundation/CQRS` now holds only the generic, database-agnostic
  contracts and dispatchers: `ICommand`, `IQuery`, `CommandDispatcher`,
  `QueryDispatcher`.
- **Fixed a latent, unrelated build gap**: `CommandDispatcher`/`QueryDispatcher`
  use `Microsoft.Extensions.DependencyInjection`'s `IServiceProvider`/
  `GetRequiredService` but `Foundgine.Foundation.csproj` never referenced the
  package. Added `Microsoft.Extensions.DependencyInjection.Abstractions`.

Not yet done: architecture tests that enforce these rules automatically (e.g.
"Abstractions must not reference anything") instead of relying on this
document; and the SQL, Graph, and Cache execution providers, the
recursive-CTE graph strategy, and mutation-merge translation are still real
`NotImplementedException` placeholders — see "Tests" below for what's
actually covered now vs. still a stub.

## Layout

```
Foundgine/
├── Foundgine.sln
├── src/
│   ├── Foundgine.Abstractions/     platform contracts (IEntity, IPlanner, IOptimizer, ...)
│   ├── Foundgine.Foundation/       Guard/Result/Optional primitives + generic CQRS
│   ├── Foundgine.Metadata/         entity/field/column/relationship metadata model
│   ├── Foundgine.Diagnostics/      diagnostic events, scopes, listeners
│   ├── Foundgine.Builders/         generic query-plan tree + builder infrastructure
│   ├── Foundgine.Execution.Contracts/  execution context, ProviderPlan, IExecutionProvider
│   ├── Foundgine.Core/             mutation plans + the concrete SQL/Graph/Cache execution providers
│   ├── Foundgine.Reflection/       (placeholder — see project README)
│   ├── Foundgine.Serialization/    (placeholder — see project README)
│   │
│   ├── Graphgine/                  GraphQL mapping, SQL graph structures, execution compilers
│   ├── Graphgine.HotChocolate/     the only project allowed to reference HotChocolate directly
│   ├── Graphgine.AspNetCore/       (placeholder — see project README)
│   ├── Graphgine.Analyzers/        (placeholder — see project README)
│   └── Graphgine.SourceGenerators/ Roslyn generator for mapping-derived metadata
│
├── samples/
│   └── Graphgine.Samples.Banking/  the former HotChocolateCoffeeBeanery example app
├── tests/                          one real xunit project per testable layer
│   ├── Foundgine.Foundation.Tests/
│   ├── Foundgine.Metadata.Tests/
│   ├── Foundgine.Builders.Tests/
│   ├── Foundgine.Diagnostics.Tests/
│   ├── Foundgine.Execution.Contracts.Tests/
│   ├── Foundgine.Core.Tests/
│   ├── Graphgine.Tests/
│   └── Graphgine.HotChocolate.Tests/
├── benchmarks/                     empty — no benchmark project existed to migrate
├── docs/
└── legacy/
    └── CoffeeBeanery/              the original monolithic library, unmodified
```

## How this was assembled — read this before you build

This restructuring is based on a prototype that was already partially built inside
this repo, under `example/HotChocolateCoffeeBeanery/Domain/` (`CoffeeBeanery.Foundation`,
`CoffeeBeanery.Runtime`, `CoffeeBeanery.GraphQL`, `CoffeeBeanery.Mapping.Generators`).
That prototype already implemented almost exactly the platform/product split
described above, so it — not the older monolithic `src/CoffeeBeanery/` library —
is what got renamed and promoted into `src/` here. The monolithic library is
GraphQL-and-SQL code with no clean seam between "generic platform" and
"GraphQL product," so it wasn't a good source to split from directly; it's
preserved as-is under `legacy/CoffeeBeanery/` rather than discarded.

Because of that:

- **Real, working content**: `Foundgine.Abstractions/.Foundation/.Metadata/.Diagnostics/.Builders/.Core`,
  `Graphgine`, `Graphgine.HotChocolate`, `Graphgine.SourceGenerators`, and the
  `Graphgine.Samples.Banking` sample all carry real migrated code with namespaces
  mechanically rewritten (e.g. `CoffeeBeanery.GraphQL.Core.Foundation.Metadata` →
  `Foundgine.Metadata`, `CoffeeBeanery.GraphQL.Core.Runtime` → `Graphgine.Execution`).
- **Placeholders**: `Foundgine.Reflection`, `Foundgine.Serialization`,
  `Graphgine.AspNetCore`, and `Graphgine.Analyzers` have no corresponding code in
  the source repo to extract yet. Each has a `README.md` explaining what belongs
  there and why it's empty for now, rather than guessed-at filler code.
- **Stale references carried over**: the original prototype referenced two
  projects that don't exist anywhere in the repo (`Domain.Shared`,
  `CoffeeBeanery.Runtime.Postgres`). Those references were dropped/commented
  with a note at the point they occurred instead of silently fabricated.
- **Not verified to `dotnet build`**: the prototype this is based on wasn't a
  complete, buildable solution to begin with (see above), so this restructuring
  preserves its actual completeness level under the new names/paths rather than
  claiming a green build that wouldn't reflect reality. Treat this as a structural
  migration, not a build-verified one — plan to fix compile errors as you wire it
  up, the same way you would have in the original prototype.

See `docs/MIGRATION.md` for the full file-by-file mapping.

## Tests

Every project with real, non-trivial logic has its own xunit test project in
`tests/`, named after the layer it covers (`Foundgine.Metadata` →
`Foundgine.Metadata.Tests`, etc.), rather than one catch-all test project —
so a change to a single layer only needs to touch and re-run its own tests.

- `Foundgine.Foundation.Tests` — `Guard`/`Optional`/`Result`/`ValueList`, and the
  CQRS `CommandDispatcher`/`QueryDispatcher` against a real `IServiceProvider`.
- `Foundgine.Metadata.Tests` — strong-id equality/hashing, `MetadataRegistry`,
  and `JoinGraph`'s forward/reverse edge indexing.
- `Foundgine.Builders.Tests` — `QueryNodeBuilder.ScanComposite` across
  single-entity, two-entity, and three-entity (left-deep join chain) models.
- `Foundgine.Diagnostics.Tests` — verifies `FoundgineDiagnostics`' `ActivitySource`
  and `Meter` actually emit activities/measurements when a listener is attached.
- `Foundgine.Execution.Contracts.Tests` — the context/options/result/row records,
  the `ProviderNode` hierarchy, and `IExecutionProvider` against a stub implementation.
- `Foundgine.Core.Tests` — `MutationPlan`/`MutationOperation`, and documents (via
  `Assert.ThrowsAsync<NotImplementedException>`) that the Sql/Cache/Graph providers
  are still unimplemented skeletons, so that test starts failing the moment one
  of them gets a real body and needs its own real test written.
- `Graphgine.Tests` — `ExpressionHelper.GetMemberName`'s lambda parsing, the
  `Sql` value objects (`EntityKey`, `Pagination`, `UpsertKey`, ...), `FilterValue`,
  and both `IGraphStrategy` implementations (Apache AGE Cypher generation, and
  that the recursive-CTE strategy still throws `NotImplementedException` everywhere).
- `Graphgine.HotChocolate.Tests` — `WhereCompiler` and `OrderCompiler` against
  hand-built `HotChocolate.Language` AST nodes (no live GraphQL server needed),
  covering scalar shorthand, explicit operators, `and`/`or`, navigation, and
  `some`/`all`/`none` collection filters.

`Foundgine.Abstractions` (pure interfaces) and the four placeholder projects
(`Foundgine.Reflection`, `Foundgine.Serialization`, `Graphgine.AspNetCore`,
`Graphgine.Analyzers`) have no test project yet, for the same reason they have
no real code yet — see each one's own `README.md`. `Graphgine.SourceGenerators`
also has no test project: testing a Roslyn incremental generator properly needs
`Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing`-style harness tests
that snapshot generated output, which is a different shape of test than the
rest of this list and deliberately left as a follow-up rather than done half-way.

**Not restored or run.** This sandbox has no network access to nuget.org, so
none of these were actually `dotnet test`-verified — they're written directly
against the real public API of each file (verified by reading the source, not
guessed), but treat them the same as the rest of the migration: check they
compile and pass once you restore, and expect to fix the odd signature
mismatch (HotChocolate.Language's exact node constructors in particular).

### Integration tests

Everything above is unit-level: no database, no running server. The
integration coverage lives outside `tests/`, next to the sample it exercises,
in `samples/Graphgine.Samples.Banking/Test/` — an Apidog project/CLI-export
suite that runs GraphQL `wrapper` mutations/queries against a live
`Api.Banking` instance backed by real Postgres/AGE, covering the relational
path, the graph-traversal path, and filtering. See that folder's own
`README.md` for what each scenario covers and how to run it.
