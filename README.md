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
│ Planning              │
│ Providers             │
│ Reflection            │
│ Serialization         │
└─────────────────────┘
     ▲
     │
   .NET
```

## Dependency direction

Strict rule, enforced by the `ProjectReference`s in every `.csproj` (verified —
see "Architecture fixes" below) and by `tests/Foundgine.Tests/ArchitectureTests.cs`,
which parses the `.csproj` graph directly and fails the build if a disallowed
`ProjectReference` or a dependency cycle is introduced:

```
Graphgine
    │
    ▼
Foundgine.Planning              (MutationOperation, MutationPlan)
    ▼
Foundgine.Execution.Contracts   (ExecutionContext, ProviderPlan, IExecutionProvider)
    ▲
    │
Foundgine.Providers             (SqlExecutionProvider, GraphExecutionProvider, CacheExecutionProvider)
    ▼
Foundgine.Builders / Foundgine.Metadata
    ▼
Foundgine.Foundation
    ▼
Foundgine.Abstractions
```

`Foundgine.Planning` and `Foundgine.Providers` are peers, not a chain — they
used to be two subfolders of a single `Foundgine.Core` project
(`Foundgine.Core.MutationPlan` and `Foundgine.Core.Provider`) that never
referenced each other's types. Once nothing in the repo took a dependency on
both halves of `Core` for a shared reason, keeping them bundled was just the
"put it somewhere" catch-all the root README used to warn against, so they
were split into their own projects along the seam that already existed.
`Graphgine` only ever needed the mutation-planning half.

Foundgine never references Graphgine — not in any `.csproj`, and not in any
`using` inside a `.cs` file either. Graphgine is simply a consumer of the
platform, the same way a second product would be.

### Architecture fixes applied to the migrated code

The mechanical namespace rewrite from the CoffeeBeanery prototype carried over
a few dependency-direction violations that the `ProjectReference`s alone
didn't catch (mostly stray `using`s and types that were in the wrong project
to begin with). These have been fixed:

- **`IExecutionProvider` no longer lives in `Foundgine.Abstractions`.** It
  needs `ProviderPlan`/`ExecutionContext`, which would otherwise pull the
  provider-implementation layer in as a dependency of the bottom-most project
  — exactly backwards. Those types (`ExecutionContext`, `ExecutionOptions`,
  `ExecutionResult`, `ExecutionStatistics`, `ExecutionRow`, `ProviderPlan`,
  `ProviderNode`, `IExecutionProvider`) now live together in a new project,
  **`Foundgine.Execution.Contracts`**, which depends only on
  `Foundgine.Metadata`. `Foundgine.Providers` depends on it (for its concrete
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

Not yet done, and worth doing before adding features rather than after:
architecture tests that enforce these rules automatically (e.g. "Abstractions
must not reference anything") instead of relying on this document; the SQL,
Graph, and Cache execution providers, the recursive-CTE graph strategy, and
mutation-merge translation are still real `NotImplementedException`
placeholders, not just naming issues; and `Foundgine.Tests`/`Graphgine.Tests`
are still empty scaffolding rather than actual coverage.

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
│   ├── Foundgine.Planning/         mutation plans (MutationOperation, MutationPlan) — a mutation isn't a query
│   ├── Foundgine.Providers/        the concrete SQL/Graph/Cache execution providers (not yet implemented)
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
│   ├── Foundgine.Samples.Banking/  same domain, no GraphQL — see its own README
│   └── Graphgine.Samples.Banking/  the former HotChocolateCoffeeBeanery example app
├── tests/
│   ├── Foundgine.Tests/            architecture/dependency-direction tests + a placeholder for the rest
│   └── Graphgine.Tests/            placeholder xunit project
├── benchmarks/                     empty — no benchmark project existed to migrate
└── docs/
```

> The original monolithic `CoffeeBeanery` library previously lived under
> `legacy/CoffeeBeanery/` during the platform/product split. It has been removed
> ahead of the first public release so the repository reflects the current
> architecture rather than the prototype it was extracted from — nothing in
> `src/`, `samples/`, or `tests/` referenced it. Its full history, including the
> original code, is preserved in git (see any commit before its removal).

## How this was assembled — read this before you build

This restructuring is based on a prototype that was already partially built inside
this repo, under `example/HotChocolateCoffeeBeanery/Domain/` (`CoffeeBeanery.Foundation`,
`CoffeeBeanery.Runtime`, `CoffeeBeanery.GraphQL`, `CoffeeBeanery.Mapping.Generators`).
That prototype already implemented almost exactly the platform/product split
described above, so it — not the older monolithic `src/CoffeeBeanery/` library —
is what got renamed and promoted into `src/` here. The monolithic library is
GraphQL-and-SQL code with no clean seam between "generic platform" and
"GraphQL product," so it wasn't a good source to split from directly. It was
kept as-is under `legacy/CoffeeBeanery/` during the split for reference, then
removed ahead of the first public release (see the note in the tree diagram
above) once nothing else in the repo depended on it.

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
