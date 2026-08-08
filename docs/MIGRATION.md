# Migration mapping

Source → destination, and why.

## Platform (from `example/.../Domain/Foundgine.Foundation/`)

| Old namespace | New namespace / project |
|---|---|
| `...Foundation.Abstractions` | `Foundgine.Abstractions` |
| `...Foundation.Common` | `Foundgine.Foundation` |
| `...Foundation.Metadata` | `Foundgine.Metadata` |
| `...Foundation.Diagnostics` | `Foundgine.Diagnostics` |
| `...Foundation.QueryPlan` | `Foundgine.Builders` |
| `...Foundation.MutationPlan` | `Foundgine.Core` (`Foundgine.Core.MutationPlan`) |
| `...Foundation.ProviderPlan` | `Foundgine.Execution.Contracts` (moved a second time — see "Post-merge architecture fixes" below) |
| `...Foundation.Runtime` | `Foundgine.Execution.Contracts` (moved a second time — see below) |
| `...Foundation.Provider` | `Foundgine.Core` (`Foundgine.Core.Provider`) |

`QueryPlan` became its own project (`Foundgine.Builders`) rather than folding into
`Foundgine.Core`, because it's pure tree/builder infrastructure with no execution
concerns — it matches "Builder infrastructure" in the target architecture more
than "Pipeline abstractions."

## Product (from `example/.../Domain/Foundgine.Runtime/`)

| Old namespace | New namespace / project |
|---|---|
| `Foundgine.CQRS` | `Foundgine.Foundation.CQRS` — generic CQRS, not GraphQL-specific, moved to the platform |
| `Foundgine.Service` (`QueryResult<M>`) | `Graphgine` |
| `...GraphQL.Core.Mapping` | `Graphgine.Mapping` |
| `...GraphQL.Core.Sql` | `Graphgine.Sql` |
| `...GraphQL.Core.Runtime` (+ `.Filtering`, `.Paging`) | `Graphgine.Execution` (+ `.Filtering`, `.Paging`) |

## HotChocolate adapter (from `example/.../Domain/Foundgine.GraphQL/`)

| Old file | New location |
|---|---|
| `Adapter/HotChocolateAdapter.cs` | `src/Graphgine.HotChocolate/` (namespace stays `Graphgine.Execution` by design — see the file's own header comment: it's the one file allowed to live outside its "home" namespace so only this assembly touches HotChocolate types) |
| `Adapter/ContextResolverHelper.cs` | `src/Graphgine.HotChocolate/`, namespace `Graphgine.HotChocolate` |
| `Adapter/FilterQueryExtension.cs`, `Adapter/WhereCompiler.cs` | `src/Graphgine.HotChocolate/`, namespace `Graphgine.Execution.Filtering` |
| `Mutation/*.cs`, `Query/WrapperQueryResolver.cs` | **`samples/Graphgine.Samples.Banking/Api/Api.Banking/`** — these declared `namespace Api.Banking.Mutation` / `Api.Banking.Query`, i.e. they're sample-app resolvers, not library code, even though they physically sat inside the library project before |

## Source generator (from `example/.../Domain/Foundgine.Mapping.Generators/`)

Moved wholesale to `src/Graphgine.SourceGenerators/`, namespace
`Foundgine.GraphQL.Core.Mapping.Generators` → `Graphgine.SourceGenerators`.
Kept as `netstandard2.0` (required for Roslyn components) and does **not**
`ProjectReference` any other Foundgine/Graphgine project, same as before — a
source generator can't take a normal project dependency and still run in the
consumer's compilation; it emits code that references `Foundgine.Metadata` types
by name instead (see `Emit/IdEmitter.cs`).

## Everything else

`samples/Graphgine.Samples.Banking/{Api,Domain/Domain.Model,Infrastructure,Test}`
moved to `samples/Graphgine.Samples.Banking/` with the same internal layout,
`ProjectReference` paths repointed at `src/`, and the same namespace rewrite
applied. `Program.cs`'s `services.AddCoffeeBeanery<T>(...)` call was renamed to
`services.AddGraphgine<T>(...)` for consistency, though that extension method
itself doesn't have a home yet (see `src/Graphgine.AspNetCore/README.md`).

`src/Foundgine/` (the older monolithic library, ~5,000 lines, no clean
platform/product seam) was **not** split — it's preserved unmodified under
`legacy/Foundgine/`. It duplicates a lot of what's in `Graphgine`/`Foundgine.Core`
now (its own `Mapper`, `NodeMap`, `SqlQueryCompiler`, etc.) and should probably be
deleted once the new structure is confirmed to cover its use cases — kept for now
so nothing was silently lost.

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
`Foundgine.Runtime.Postgres` reference this doc previously flagged as stale
is now moot — Postgres-specific code (the old `GraphStrategy`, `PostgresSqlWriter`,
`SqlFilterEmitter`/`SqlFilterParameterBag`, and now `UnitOfWork`) all lives in
`Graphgine.Sql`.
