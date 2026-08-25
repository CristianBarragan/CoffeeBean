# Foundgine 1.0.0

Foundgine 1.0.0 is the current shipped release. It carries forward the entire 0.5.0 release surface (see [Release 0.5.0](RELEASE-0.5.0.md)) unchanged — no `src/` package's implementation changed in this release. 1.0.0 is a semver-stability milestone: the public API documented in Release 0.5.0 is now under a `1.x` stability commitment. See [CHANGELOG.md](../CHANGELOG.md) for the itemized change list.

## Release gates

No `src/` package source changed in this release, so the restore/build/test evidence recorded for 0.5.0 still applies to the packages themselves:

```text
dotnet restore   ✓ (unchanged since 0.5.0)
dotnet build     ✓ (unchanged since 0.5.0)
dotnet test      ✓ (unchanged since 0.5.0)
```

This release does include one code change outside `src/`: a missing analyzer reference fixed in `samples/Foundgine.SupplyChain/Domain/Foundgine.SupplyChain.Domain.csproj` (see CHANGELOG). **That fix has not been verified with `dotnet build`/`dotnet test` in the environment that prepared this release** — no .NET SDK was available there. Run `dotnet build samples/Foundgine.SupplyChain/Foundgine.SupplyChain.sln` (and the sample's test project) in CI or locally before tagging and publishing packages from this release.

The repository also contains separate PostgreSQL integration and CoffeeBeanery benchmark workflows. Those require their own database/runtime environment and should be reported separately from the normal solution test gate.

## What's new since 0.5.0

- **Semver stability commitment.** `VersionPrefix` moves to `1.0.0`. This is a declaration about the surface, not a new capability: everything in [Release 0.5.0's core release surface](RELEASE-0.5.0.md#core-release-surface) is what 1.0.0 stabilizes.
- **`samples/Foundgine.SupplyChain` compiles again.** The `Domain` project was missing the `Foundgine.Aot.Generator` analyzer reference needed to actually run the AOT source generator over its `[FoundgineModel]`/`[FoundgineEntity]`/`[FoundgineField]`-attributed types, so downstream projects failed with `CS0234` on `Foundgine.Generated`. Fixed by adding the analyzer `ProjectReference`, matching the existing pattern in `tests/Foundgine.Aot.Tests` and `tests/Foundgine.E2E.Tests`.
- **New "Getting started" documentation page** at `docs-site/getting-started/` — a hands-on tutorial that runs the Supply Chain sample end to end and walks its ten architectural layers, following the sample's `GUIDE.md`.
- **Homepage rendering fix.** A markup issue in `docs-site/index.html` that could make the three homepage "problem" callouts run together as single words is fixed.

## Core release surface

Unchanged from [Release 0.5.0](RELEASE-0.5.0.md#core-release-surface). 1.0.0 adds no new packages, providers, or public API surface.

## What "1.0.0" means here

Moving to `1.0.0` is a statement that the public API surface described in Release 0.5.0 is considered stable: future breaking changes to that surface should be reserved for a `2.0.0`, and additive, backward-compatible changes should land as `1.x.0` per normal semver. It is **not** a claim that new functionality shipped, and it is **not** a claim that anything was re-verified beyond what's stated in the release gates above.

## Deliberate non-claims

Unchanged from 0.5.0. Foundgine is still not an autonomous-agent runtime, a workflow/orchestration engine, a universal provider abstraction with full cross-provider feature parity, an ORM replacement, an identity/authorization provider, or a universally faster alternative to EF Core, GraphQL servers, or other execution stacks.

## Evidence policy

Behavioral claims should be backed by active tests. Performance claims should identify the workload, fixture, concurrency, provider versions, and measurement method. Historical design documents explain prior decisions but are not release specifications.
