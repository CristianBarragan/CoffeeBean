# Foundgine.SupplyChain — collapsed into one project

Same sample, same logic, byte-for-byte identical `.cs` files — just moved from
6 `.csproj` files into folders inside 1.

| | Before | After |
|---|---|---|
| Projects | Api, Application, Domain, Infrastructure, Semantics (+Tests) | 1 |
| `.csproj` files to maintain | 5 (+1 for tests) | 1 |
| `DependencyInjection.cs` indirection files | 2 | 0 (inlined into `Program.cs`) |
| `.sln` project entries | 6 | 1 |

## Why this is safe

The layer split in the original sample is an *architectural choice*, not a
requirement of the framework:

- **The AOT source generator** (`Foundgine.Aot.Generator`) is a Roslyn
  analyzer. Analyzers run over whatever *project* they're attached to and see
  every file in it — folders don't change that. Domain never had to be a
  separate assembly for `[FoundgineModel]`/`[FoundgineEntity]` attributes to
  be picked up; it only had to be *compiled in the same project* as the
  generator reference, which it still is here.
- **Application/Infrastructure separation** existed so you could swap the
  data-access layer (e.g. Postgres → another provider) without touching
  business logic. That's still true with folders — `Infrastructure/` and
  `Application/` are just as swappable, you've only removed the ceremony of
  a project boundary (and the two `AddXxx()` extension-method files whose
  only job was gluing project boundaries back together with DI).

## What I did NOT change

- Still targets Postgres (same `SupplyChainConnectionString` env var / config
  key) — swapping to `Foundgine.InMemory` is a separate, larger change
  (different query executor, no SQL compiler) and would need its own pass.
- `Tests/` is left out of this folder on purpose: test projects are a
  different concern from "reduce app-layer boilerplate" and normally stay
  separate regardless of how the app itself is organized.

## Layout

```
Foundgine.SupplyChain.Simple.csproj   <- was 5 .csproj files
Program.cs                            <- Api/Program.cs + both DI extension methods inlined
Domain/            (Models, Mappings, StorageModels)
Semantics/          (SupplyChainSemanticModel)
Application/        (Contracts, Authorization, SupplyChainApplication)
Infrastructure/
  Queries/          (SemanticSqlQueryExecutor, SupplyChainQueryRepository)
  Mutations/        (SupplyChainMutationRepository)
```

Drop this folder in next to `src/` (same relative depth the original
`samples/Foundgine.SupplyChain/` folder was at — three `../../../` up to
`src/`) and it builds as one deployable unit.
