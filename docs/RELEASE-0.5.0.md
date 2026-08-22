# Foundgine 0.5.0

Foundgine 0.5.0 is the current shipped release. It carries forward the 0.4.0 release gates and core release surface (see [Release 0.4.0](RELEASE-0.4.0.md)) and is a refactoring/cleanup release: no new runtime capability, but a meaningfully cleaner package structure and documentation set. See [CHANGELOG.md](../CHANGELOG.md) for the itemized change list.

## Release gates

The current source tree has passed:

```text
dotnet restore   ✓
dotnet build     ✓
dotnet test      ✓
```

The repository also contains separate PostgreSQL integration and CoffeeBeanery benchmark workflows. Those require their own database/runtime environment and should be reported separately from the normal solution test gate.

## What's new since 0.4.0

0.5.0 is entirely refactoring and documentation work, triggered by a structural review of the `samples/Foundgine.HighAssurance.Postgres` sample.

- **`Foundgine.Authorization` is now a real library, not sample code.** The authorization recovery control plane — witness quorum, credential lifecycle, journal reconciliation, and failover — used to live inside `samples/Foundgine.HighAssurance.Postgres/Authorization/` and `.../Execution/`. Of the 49 files there, only 4 actually touch Postgres/Npgsql. The other 45 were pure, provider-agnostic C# with no dependency on Postgres, Banking, or anything in `src/` referencing them — invisible to the rest of the framework, sitting inside one sample.
  - That sample project also had no `IsPackable` set, so — unlike its sibling `Foundgine.HighAssurance.Banking`, which explicitly opts out with `IsPackable=false` — it inherited the shared package-metadata defaults and was being packed and published to NuGet by CI's unfiltered `dotnet pack Foundgine.sln`, alongside the real library packages. That was almost certainly unintentional: the code's own doc comments repeatedly describe it as a "reference/test implementation" that "production deployments must replace."
  - 43 of the 45 files moved into a new project, `src/Foundgine.Authorization/`, with a single `Foundgine.Authorization` namespace, packaged the same way as the other `src/` libraries, depending only on `Foundgine.Execution` and the BCL.
  - The remaining 2 — `AuthorizationDecision.cs` and `AuthorizationExecutionBinding.cs` — looked like part of the same generic set but actually hardcode the `transferFunds` operation and take `BankAccount`/`TransferFundsCommand` parameters from the Banking sample. Moving them into `src/` would have made a source library depend on a samples project, so they stayed in the sample alongside the 4 genuinely Postgres-specific files (`PostgresAuthorizationContextStore`, `PostgresAuthorizationRecoveryCoordinator`, `PostgresAuthorizationSecurityUnitOfWork`, `PostgresTransferFundsExecutor`), which were confirmed to be concrete, non-generic Npgsql implementations with no hidden abstraction layer to extract.
- **Fixed a genuinely malformed `Foundgine.sln`.** Independent of the code move, the solution file's `ProjectConfigurationPlatforms` section had a duplicate configuration block sitting outside any `GlobalSection` — invalid solution syntax — and the Banking/Postgres sample projects were missing their `Release|Any CPU` build configuration entirely. Both are fixed, and the new `Foundgine.Authorization` project is registered correctly.
- **Milestone-numbering scheme removed from public surfaces.** Internal `M<n>.<n>` tracking IDs were embedded throughout doc comments, README section headers, and changelog entries. They carried no meaning outside the original development process and are now gone from code comments and prose; section headers use plain descriptive titles.
- **Documentation index rewritten.** `docs/README.md` linked to files that don't exist anywhere in this repository. It's now an accurate index of what's actually there. The same dead-link and stale-path cleanup was applied to `docs/ROADMAP.md`, `docs/SECURITY.md`, `docs/security/CHANGELOG.md`, `README.md`, `ai.seo.md`, and `llms-full.md`.

## Core release surface

Everything listed in [Release 0.4.0](RELEASE-0.4.0.md#core-release-surface) remains true, plus:

- a standalone, packaged `Foundgine.Authorization` library for authorization recovery control-plane concerns (witness quorum, credential lifecycle, journal reconciliation, failover), usable independently of any specific provider sample;
- a corrected, valid `Foundgine.sln` with complete build configurations for every project.

## Deliberate non-claims

0.5.0 does not change the 0.4.0 non-claims. Foundgine is still not an autonomous-agent runtime, a workflow/orchestration engine, a universal provider abstraction with full cross-provider feature parity, an ORM replacement, an identity/authorization provider, or a universally faster alternative to EF Core, GraphQL servers, or other execution stacks.

## Evidence policy

Behavioral claims should be backed by active tests. Performance claims should identify the workload, fixture, concurrency, provider versions, and measurement method. Historical design documents explain prior decisions but are not release specifications.
