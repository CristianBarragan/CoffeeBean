# NuGet packaging

Foundgine is packaged as separate NuGet packages from the packable projects under `src/`.

## Package documentation

Every public package has its own `README.md` in its project directory. The package README is copied into the `.nupkg` as `README.md`.

This is intentional: a user landing on `Foundgine.Sql` or `Foundgine.Aot` should see documentation for that package rather than the repository-wide README. Package README links use absolute GitHub URLs so they remain valid when rendered on NuGet.org.

The package set is:

- `Foundgine`
- `Foundgine.Abstractions`
- `Foundgine.Metadata`
- `Foundgine.Semantics`
- `Foundgine.Planning`
- `Foundgine.Execution`
- `Foundgine.InMemory`
- `Foundgine.Sql`
- `Foundgine.Aot`
- `Foundgine.Intent.Json`
- `Foundgine.GraphQL.HotChocolate`
- `Foundgine.GraphQL.HotChocolate.Mutations`

`Foundgine.Aot.Generator` is a build-time Roslyn component and is intentionally not published as a standalone NuGet package.

## AOT package

`Foundgine.Aot` contains the runtime attributes/contracts plus the Roslyn generator as an analyzer:

```text
Foundgine.Aot.nupkg
  README.md
  lib/net9.0/Foundgine.Aot.dll
  lib/net9.0/Foundgine.Metadata.dll
  analyzers/dotnet/cs/Foundgine.Aot.Generator.dll
```

The generator is deliberately **not** a runtime/reference dependency of `Foundgine.Aot`. It runs in the consuming application's compilation and generates `Foundgine.Generated.GeneratedMetadata` there. `Foundgine.Metadata.dll` is bundled in the AOT package as the runtime metadata dependency, so the package does not also declare a separate `Foundgine.Metadata` NuGet dependency.

## Local packaging

Restore/build first, then run:

```powershell
./eng/pack.ps1 -Version 0.1.0
```

Packages are written to `artifacts/nuget`.

## CI

`.github/workflows/build.yml` restores, builds, tests, packs every packable project, validates the AOT package layout, validates that every package contains a package-specific README, and uploads all packages as a workflow artifact.

A tag such as `v0.1.0` publishes the packages to NuGet.org through **NuGet Trusted Publishing / OIDC**. No long-lived NuGet API key is required by the workflow.

The GitHub Actions job must have:

- `id-token: write` permission;
- the NuGet Trusted Publishing policy configured for the NuGet package owner;
- the correct policy creator username passed to `NuGet/login@v1`; and
- the GitHub environment, repository, and workflow constraints configured to match that policy.

The current workflow uses the `production` environment and the NuGet policy creator username `dero`.
