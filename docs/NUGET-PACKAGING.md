# NuGet packaging

Foundgine is packaged as separate NuGet packages from the projects under `src/`.

## AOT package

`Foundgine.Aot` contains the runtime attributes/contracts plus the Roslyn generator as an analyzer:

```text
Foundgine.Aot.nupkg
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

`.github/workflows/build.yml` restores, builds, tests, packs every packable project, validates the AOT package layout, and uploads all packages as a workflow artifact.

A tag such as `v0.1.0` publishes the packages to NuGet.org. The repository must have a `NUGET_API_KEY` GitHub Actions secret configured for tag publishing.
