# Archive

Nothing here is deleted or abandoned — it's future work, moved out of the way
so the active tree only contains what's needed to prove the Foundgine thesis:

> Can Foundgine describe a domain, allow a dynamic planner to reason over it,
> and hand an execution plan to a provider?

Everything below is a candidate to come back out once the first Banking E2E
(`Customer -> Account -> Transaction` through Metadata -> Planner ->
QueryPlan -> SQL Provider -> real DB) passes.

## src/

| Moved | Why |
|---|---|
| `Graphgine`, `Graphgine.HotChocolate`, `Graphgine.AspNetCore`, `Graphgine.Analyzers`, `Graphgine.SourceGenerators`, `Graphgine.Postgres` | GraphQL-facing layer. A future *consumer* of Foundgine, not a requirement for proving it. |
| `Foundgine.Core` | Former grab-bag project. Nothing in the active tree references it. |
| `Foundgine.Reflection`, `Foundgine.Serialization` | Infrastructure that isn't on the critical path for the first E2E. |
| `Foundgine.Providers.Extra/` (`GraphExecutionProvider.cs`, `CacheExecutionProvider.cs`) | Pulled out of `Foundgine.Providers` so that project builds only the SQL provider. Both were unimplemented skeletons (`NotSupportedException`) — drop them back in once the SQL pipeline works and a second provider is actually needed. |

## samples/

| Moved | Why |
|---|---|
| `Graphgine.Samples.Banking` (and its `Api.Banking`, `Domain.Model`, `Database.*` sub-projects) | The GraphQL-facing banking sample. `Foundgine.Samples.Banking` (kept active) is now the single canonical proof domain — no need to maintain two banking samples in parallel. |

## tests/

| Moved | Why |
|---|---|
| `Foundgine.Core.Tests` | Tests for the archived `Foundgine.Core` project. |
| `Graphgine.Tests`, `Graphgine.HotChocolate.Tests` | Tests for the archived Graphgine layer. |

## legacy/, benchmarks/

Moved as-is. Neither was part of the active dependency graph and neither is
required for the first E2E; `benchmarks/` is explicitly on the "don't touch
until E2E works" list.

## Bringing something back

1. `git mv archive/src/<Project> src/<Project>` (or the equivalent for
   `samples/`/`tests/`).
2. Add the `.csproj` back into `Foundgine.sln`.
3. Re-add any `ProjectReference`s it needs.

Nothing here was deleted from git history — `git log --follow` still works
on every file.
