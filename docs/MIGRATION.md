# Migration mapping

Source → destination, and why.

## Platform (from `legacy/HotChocolateCoffeeBeanery/.../Domain/CoffeeBeanery.Foundation/`)

| Old namespace | New namespace / project |
|---|---|
| `...Foundation.Abstractions` | `Foundgine.Abstractions` |
| `...Foundation.Common` | `Foundgine.Foundation` |
| `...Foundation.Metadata` | `Foundgine.Metadata` |
| `...Foundation.Diagnostics` | `Foundgine.Diagnostics` |
| `...Foundation.QueryPlan` | `Foundgine.Builders` |
| `...Foundation.MutationPlan` | `Foundgine.Planning` (moved a second time, out of `Foundgine.Core.MutationPlan` — see "Foundgine.Core split" below) |
| `...Foundation.ProviderPlan` | `Foundgine.Execution.Contracts` (moved a second time — see "Post-merge architecture fixes" below) |
| `...Foundation.Runtime` | `Foundgine.Execution.Contracts` (moved a second time — see below) |
| `...Foundation.Provider` | `Foundgine.Providers` (moved a second time, out of `Foundgine.Core.Provider` — see "Foundgine.Core split" below) |

`QueryPlan` became its own project (`Foundgine.Builders`) rather than folding into
`Foundgine.Core`, because it's pure tree/builder infrastructure with no execution
concerns — it matches "Builder infrastructure" in the target architecture more
than "Pipeline abstractions."

## Product (from `legacy/HotChocolateCoffeeBeanery/.../Domain/CoffeeBeanery.Runtime/`)

| Old namespace | New namespace / project |
|---|---|
| `CoffeeBeanery.CQRS` | `Foundgine.Foundation.CQRS` — generic CQRS, not GraphQL-specific, moved to the platform |
| `CoffeeBeanery.Service` (`QueryResult<M>`) | `Graphgine` |
| `...GraphQL.Core.Mapping` | `Graphgine.Mapping` |
| `...GraphQL.Core.Sql` | `Graphgine.Sql` |
| `...GraphQL.Core.Runtime` (+ `.Filtering`, `.Paging`) | `Graphgine.Execution` (+ `.Filtering`, `.Paging`) |

## HotChocolate adapter (from `legacy/HotChocolateCoffeeBeanery/.../Domain/CoffeeBeanery.GraphQL/`)

| Old file | New location |
|---|---|
| `Adapter/HotChocolateAdapter.cs` | `src/Graphgine.HotChocolate/` (namespace stays `Graphgine.Execution` by design — see the file's own header comment: it's the one file allowed to live outside its "home" namespace so only this assembly touches HotChocolate types) |
| `Adapter/ContextResolverHelper.cs` | `src/Graphgine.HotChocolate/`, namespace `Graphgine.HotChocolate` |
| `Adapter/FilterQueryExtension.cs`, `Adapter/WhereCompiler.cs` | `src/Graphgine.HotChocolate/`, namespace `Graphgine.Execution.Filtering` |
| `Mutation/*.cs`, `Query/WrapperQueryResolver.cs` | **`samples/Graphgine.Samples.Banking/Api/Api.Banking/`** — these declared `namespace Api.Banking.Mutation` / `Api.Banking.Query`, i.e. they're sample-app resolvers, not library code, even though they physically sat inside the library project before |

## Source generator (from `legacy/HotChocolateCoffeeBeanery/.../Domain/CoffeeBeanery.Mapping.Generators/`)

Moved wholesale to `src/Graphgine.SourceGenerators/`, namespace
`CoffeeBeanery.GraphQL.Core.Mapping.Generators` → `Graphgine.SourceGenerators`.
Kept as `netstandard2.0` (required for Roslyn components) and does **not**
`ProjectReference` any other Foundgine/Graphgine project, same as before — a
source generator can't take a normal project dependency and still run in the
consumer's compilation; it emits code that references `Foundgine.Metadata` types
by name instead (see `Emit/IdEmitter.cs`).

## Everything else

`legacy/HotChocolateCoffeeBeanery/{Api,Domain/Domain.Model,Infrastructure,Test}`
moved to `samples/Graphgine.Samples.Banking/` with the same internal layout,
`ProjectReference` paths repointed at `src/`, and the same namespace rewrite
applied. `Program.cs`'s `services.AddCoffeeBeanery<T>(...)` call was renamed to
`services.AddGraphgine<T>(...)` for consistency, though that extension method
itself doesn't have a home yet (see `src/Graphgine.AspNetCore/README.md`).

`src/CoffeeBeanery/` (the older monolithic library, ~5,000 lines, no clean
platform/product seam) was **not** split — it was preserved unmodified under
`legacy/CoffeeBeanery/` while the new structure was confirmed to cover its use
cases, so nothing was silently lost. That confirmation is done: the directory
has since been deleted ahead of the first public release (full history remains
in git). It duplicated what now lives in `Graphgine`/`Foundgine.Core` (its own
`Mapper`, `NodeMap`, `SqlQueryCompiler`, etc.).

## Post-merge architecture fixes

A later review caught dependency-direction violations that the initial
mechanical rename didn't — mostly stray `using`s and a couple of types parked
in the wrong project. See the README's "Architecture fixes applied to the
migrated code" section for the reasoning; the file moves were:

| From | To | Why |
|---|---|---|
| `Foundgine.Abstractions/IExecutionProvider.cs` | `Foundgine.Execution.Contracts/IExecutionProvider.cs` | Needed `ProviderPlan`/`ExecutionContext`, which pulled `Foundgine.Core` into the bottom-most project |
| `Foundgine.Core/ProviderPlan/*.cs` (`ExecutionRow`, `ProviderNode`, `ProviderPlan`) | `Foundgine.Execution.Contracts/` | Same — these are the provider contract, not the provider implementation |
| `Foundgine.Core/Runtime/*.cs` (`ExecutionContext`, `ExecutionOptions`, `ExecutionResult`, `ExecutionStatistics`) | `Foundgine.Execution.Contracts/` | Same |
| `Foundgine.Foundation/CQRS/UnitOfWork.cs`, `UnitOfWorkContext.cs` | `Graphgine.Sql/` | Postgres-specific (`NpgsqlConnection`), didn't belong in the database-agnostic foundation layer |

`Foundgine.Foundation/CQRS/IQuery.cs` also had an unused `using Graphgine.Execution;`
left over from the rename, deleted. `Foundgine.Core`, `Foundgine.Abstractions`, and
`Foundgine.Foundation`'s `.csproj` descriptions were updated to match. The
`CoffeeBeanery.Runtime.Postgres` reference this doc previously flagged as stale
is now moot — Postgres-specific code (the old `GraphStrategy`, `PostgresSqlWriter`,
`SqlFilterEmitter`/`SqlFilterParameterBag`) lives in `Graphgine.Sql`; `UnitOfWork`
moved through here on its way to `Graphgine.Postgres` — see "Graphgine.Postgres
split" below, its final destination.

## Foundgine.Core split

A later review flagged `Foundgine.Core` as the architecture's one remaining
catch-all: its `MutationPlan/` and `Provider/` subfolders had zero references
to each other's types, and only `MutationPlan` was actually consumed outside
the project (by `Graphgine`). That's the "real dependency violation" bar the
architecture's own rule (freeze the project list until one appears — see
README) sets for splitting further, so it was split along that seam:

| From | To |
|---|---|
| `Foundgine.Core/MutationPlan/*.cs` (`MutationKind`, `MutationOperation`, `MutationPlan`) | `Foundgine.Planning/` |
| `Foundgine.Core/Provider/*.cs` (`SqlExecutionProvider`, `GraphExecutionProvider`, `CacheExecutionProvider`) | `Foundgine.Providers/` |

`Graphgine.csproj`'s `ProjectReference` on `Foundgine.Core` became a reference
on `Foundgine.Planning` only — it never needed the provider implementations.
`Foundgine.Providers` keeps the same single `ProjectReference` on
`Foundgine.Execution.Contracts` that `Foundgine.Core` had. See
`tests/Foundgine.Tests/ArchitectureTests.cs` for the test that now enforces
this shape going forward.

## Graphgine.Postgres split

`Graphgine.csproj` referenced `Microsoft.EntityFrameworkCore` and
`Npgsql.EntityFrameworkCore.PostgreSQL` directly, and `Microsoft.CodeAnalysis.Common`
besides — none of which anything *live* in the project actually needed once
checked:

| File | Disposition |
|---|---|
| `Mapping/EfEntityMetadata.cs` | **Deleted.** Zero references anywhere in the repo, and it mixed runtime EF reflection (`DbContext`) with Roslyn symbols (`INamedTypeSymbol`) inside a runtime project — abandoned generator-era scaffolding, not something worth preserving. Its removal is also why `Microsoft.CodeAnalysis.Common` could come off `Graphgine.csproj`. |
| `Sql/AgeConnectionFactory.cs`, `Sql/UnitOfWork.cs`, `Sql/UnitOfWorkContext.cs` | **Moved** to a new `Graphgine.Postgres` project (namespace `Graphgine.Postgres`). Also unused anywhere today, but unlike `EfEntityMetadata` these are exactly the transaction/connection primitives a real `Foundgine.Providers.SqlExecutionProvider` implementation will need, so they were relocated rather than deleted. Fixed three real bugs while moving `UnitOfWork`/`UnitOfWorkContext`: `RollbackTranscation` wasn't decrementing the nested-transaction counter (only `CommitTransaction` was), `DisposeConnection`'s guard condition could never be true after a normal commit/rollback so cleanup never ran, and `UnitOfWorkContext.CreateUnitOfWork`/`GetConnection` could return/dereference null. |

`Graphgine.csproj` also dropped `morelinq` — a separate, unrelated dead package
reference found in the same `<ItemGroup>` while removing the others. Nothing
in the solution references `Graphgine.Postgres` yet (see its own `.csproj`
description); it exists so the transaction-management work has a real home
once `SqlExecutionProvider` stops being a stub.
