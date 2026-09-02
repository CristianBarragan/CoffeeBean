# Foundgine v2 package restructuring — what changed

This branch collapses the previous 17 `src/` packages into the consolidated v2
layout, with namespaces rewritten throughout `src/`, `tests/`, `samples/`,
`benchmarks/`, and the docs. **This was done without a .NET SDK available in
the environment that produced it, so it has not been compiled or run.**
Treat it as a strong first draft that needs a `dotnet build` / `dotnet test`
pass before merging, not a verified release.

## Historical intermediate layout

> The layout below documents the intermediate state produced by the first v2 pass. It is retained for migration history; see the follow-up section below for the final layout.


```
Foundgine.Core          Interfaces/, Semantic/ (+ Semantic/Metadata, Semantic/Planning), Serialization/
Foundgine.Runtime       Execution/, ControlPlane/, + root orchestrator (IFoundgine, FoundgineEngine, DI wiring)
Foundgine.Providers     Models/, Tools/MCP/, Storage/{Sql,InMemory,Elasticsearch,PostgresVector}/,
                        GraphQL/HotChocolate/ (secure query/mutation execution engine),
                        Foundgine.Providers.Aot.Generator/ (Roslyn analyzer, own .csproj — see below)
Foundgine.Extensions    GraphQL/HotChocolate/ (+ HotChocolate/Mutations — schema adapters and
                        query/mutation translation only; execution now lives in Providers)
Foundgine.Experimental  Aot/ (historical intermediate location; removed in the final layout)
```

Historical dependency graph (intermediate state): `Runtime → Core`, `Extensions → Core, Runtime`, `Providers → Core, Runtime, Extensions`, `Experimental → Core`.

**2026-09 follow-up restructure:** the remaining AOT and GraphQL execution pieces were consolidated into `Foundgine.Providers`. The AOT package itself was then removed entirely, as described in the follow-up section below. The GraphQL execution engine remains in `Foundgine.Providers`, while schema/translation adapters remain in `Foundgine.Extensions`.

- `Foundgine.Providers.Aot.Generator` (the Roslyn analyzer) moved from the historical Experimental location to `src/Foundgine.Providers/Foundgine.Providers.Aot.Generator/`. At this intermediate point, the AOT runtime types were still owned by `Foundgine.Experimental`; that ownership was removed in the final follow-up below.
- The GraphQL/HotChocolate **execution engine**
  (`FoundgineHotChocolateQueryExecutor`, `FoundgineHotChocolateMutationExecutor`)
  moved from `Foundgine.Extensions` (`GraphQL/HotChocolate/Execution/` and
  `GraphQL/HotChocolate/Mutations/`) to `Foundgine.Providers`
  (`GraphQL/HotChocolate/`). Schema adapters and query/mutation
  *translation* stay in `Foundgine.Extensions`, which `Foundgine.Providers`
  now references for those types.

## Follow-up: removal of `Foundgine.Experimental`

The original v2 migration described `Foundgine.Experimental` as the owner of
the AOT runtime declarations while the generator had already moved under
`Foundgine.Providers`. That intermediate state is now obsolete.

In the final v2 layout, **`Foundgine.Experimental` has been removed entirely**:

- AOT declarations and runtime helpers moved to
  `src/Foundgine.Providers/Aot/` under the `Foundgine.Providers.Aot` namespace.
- The Roslyn generator moved to
  `src/Foundgine.Providers/Foundgine.Providers.Aot.Generator/` and is now
  `Foundgine.Providers.Aot.Generator`. It remains a separate `netstandard2.0`
  build-only analyzer because source generators cannot be merged into a normal
  library assembly.
- `Foundgine.Experimental` has no project, package, namespace, or runtime role
  in the final source layout.
- The four publishable packages are `Foundgine.Core`, `Foundgine.Runtime`,
  `Foundgine.Providers`, and `Foundgine.Extensions`.

Consumers should therefore reference `Foundgine.Providers` for AOT declarations
and runtime support, and receive the generator from the `Foundgine.Providers` NuGet analyzer payload when
compile-time metadata generation is required.

## Old → new package mapping

| Old package | New location |
|---|---|
| `Foundgine` (root) | `Foundgine.Runtime` — **see deviation below** |
| `Foundgine.Abstractions` | `Foundgine.Core` (`Interfaces/`) |
| `Foundgine.Semantics` | `Foundgine.Core` (`Semantic/`) |
| `Foundgine.Metadata` | `Foundgine.Core` (`Semantic/Metadata/`) |
| `Foundgine.Planning` | `Foundgine.Core` (`Semantic/Planning/`) |
| `Foundgine.Intent.Json` | `Foundgine.Core` (`Serialization/`) |
| `Foundgine.Execution` | `Foundgine.Runtime` (`Execution/`) |
| `Foundgine.Security.Authority` | `Foundgine.Runtime` (`ControlPlane/`) |
| `Foundgine.AI` | `Foundgine.Providers` (`Models/`) |
| `Foundgine.MCP` | `Foundgine.Providers` (`Tools/MCP/`) |
| `Foundgine.Sql` | `Foundgine.Providers` (`Storage/Sql/`) |
| `Foundgine.InMemory` | `Foundgine.Providers` (`Storage/InMemory/`) |
| `Foundgine.Elasticsearch` | `Foundgine.Providers` (`Storage/Elasticsearch/`) |
| `Foundgine.Postgres.Vector` | `Foundgine.Providers` (`Storage/PostgresVector/`) |
| `Foundgine.GraphQL.HotChocolate` | `Foundgine.Extensions` (`GraphQL/HotChocolate/`) — schema/translation only |
| `Foundgine.GraphQL.HotChocolate.Execution` | `Foundgine.Providers` (`GraphQL/HotChocolate/`) — moved out of Extensions, see follow-up note above |
| `Foundgine.GraphQL.HotChocolate.Mutations` (translation) | `Foundgine.Extensions` (`GraphQL/HotChocolate/Mutations/`) |
| `Foundgine.GraphQL.HotChocolate.MutationExecution` | `Foundgine.Providers` (`GraphQL/HotChocolate/`) — moved out of Extensions, see follow-up note above |
| `Foundgine.Aot` | `Foundgine.Providers` (`Aot/`) |
| `Foundgine.Aot.Generator` | `Foundgine.Providers.Aot.Generator` (`src/Foundgine.Providers/Foundgine.Providers.Aot.Generator/`) — kept as its own `.csproj`, moved out of Experimental, see follow-up note above |

## One deliberate deviation from the requested mapping

The requested mapping put the root `Foundgine` orchestrator (`IFoundgine`,
`FoundgineEngine`, DI extensions, `MutationBuilder`, `PlanApproval`, etc.)
into `Foundgine.Core`. That project depends on `Foundgine.Execution`
(→ `Foundgine.Runtime`), while `Foundgine.Execution` itself depends on
`Foundgine.Abstractions`/`Foundgine.Semantics` (→ `Foundgine.Core`). Putting
the orchestrator in Core would make `Core → Runtime` and `Runtime → Core`
both true — a circular project reference that cannot build. I moved the
orchestrator into `Foundgine.Runtime` instead (namespace `Foundgine.Runtime`,
e.g. `Foundgine.Runtime.IFoundgine`), which keeps `Foundgine.Core` fully
foundational with zero outgoing dependencies. This is the one place I
deviated from the literal instructions, and it's the thing most worth a
second look.

## Other decisions worth knowing about

- **The AOT generator remains a separate assembly.** Roslyn source generators/analyzers must target `netstandard2.0` and ship as their own assembly loaded via `<ProjectReference OutputItemType="Analyzer" .../>`. It now lives at `src/Foundgine.Providers/Foundgine.Providers.Aot.Generator/` and is embedded as an analyzer inside the published `Foundgine.Providers` NuGet package. Repository project-reference builds may still reference it directly because analyzer project references are not transitive.
- **`InternalsVisibleTo` was consolidated per new assembly**, not carried
  over verbatim — the old fine-grained entries (e.g. `Semantics` → `Metadata`)
  were mostly *intra*-package after the merge and were dropped; genuinely
  cross-package ones were retargeted to the new assembly names. Worth a
  double-check against actual `internal` usage.
- **`PackageReference`s were unioned** per new package from the merged old
  `.csproj` files (versions already matched across the old projects, so no
  version conflicts to resolve).
- **Test/sample/benchmark projects were *not* physically moved or renamed.**
  Their internals (`using` directives, fully-qualified names, string
  literals like `InternalsVisibleTo` targets) were rewritten to the new
  namespaces, and their `ProjectReference`s now point at the 5 new `src/`
  projects (duplicate references collapsed to one). But e.g.
  `tests/Foundgine.Semantics.Tests/` is still named that on disk — only
  what it tests changed location, not the test project's own name. Consider
  a follow-up pass to rename/regroup test projects to mirror the 5 packages
  if you want full symmetry.
- **`Foundgine.sln` was regenerated from scratch** (fresh GUIDs, 4 solution
  folders: src/tests/samples/benchmarks) rather than hand-patched, since the
  old file's project paths no longer existed. If you use `dotnet sln` /
  MSBuild-specific solution folder nesting conventions, take a look before
  relying on it in CI.
- Docs (`docs/`, `docs-site/`, `README.md`, etc.) had the same exact
  package-name substitutions applied, but were **not** restructured or
  reworded beyond that — e.g. `docs/ARCHITECTURE.md` will still describe
  things sequentially rather than around the new 5-package story, and
  diagrams (`.svg`/`.puml`) were not regenerated.

## Suggested next steps

1. Get this onto a machine with the .NET 9 SDK, `dotnet build` the solution,
   and fix whatever the compiler finds (there will almost certainly be
   *something* — a full manual namespace/reference rewrite across 750+ files
   without a compiler in the loop is not going to be perfect).
2. Re-check the `InternalsVisibleTo` list against actual `internal` type
   usage now that assembly boundaries have moved.
3. Decide whether to rename the test/sample/benchmark projects to mirror
   the 5-package structure, or leave them as-is.
4. Sanity-check `docs/ARCHITECTURE.md` and `docs-site/architecture/` against
   the new structure — they were text-substituted but not re-authored.

## Final AOT packaging rule

The generator is a separate **assembly/project**, but not a separate **NuGet
package**. `Foundgine.Providers` packs
`Foundgine.Providers.Aot.Generator.dll` under `analyzers/dotnet/cs/`, so normal
NuGet consumers install only `Foundgine.Providers`. Direct analyzer
`ProjectReference` entries that remain in tests/samples exist only so
source-tree project-reference builds run the generator.
