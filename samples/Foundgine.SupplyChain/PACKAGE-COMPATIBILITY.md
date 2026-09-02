# Foundgine source compatibility

The Supply Chain sample is a **source-integrated reference sample**.

All Foundgine dependencies are wired to the repository's `src/` projects with `ProjectReference` entries. This is intentional: the sample must compile against the same implementation that is built and tested by `Foundgine.sln`.

The sample does not use Foundgine `0.5.x` NuGet packages.

Key source dependencies include:

- `src/Foundgine.Core/Foundgine.Core.Abstractions`
- `src/Foundgine.Providers/Foundgine.Providers.Aot`
- `src/Foundgine.Providers/Foundgine.Providers.Aot.Generator`
- `src/Foundgine.Runtime/Foundgine.Core.Semantic.Metadata`
- `src/Foundgine.Core/Foundgine.Core.Semantic`
- `src/Foundgine.Runtime/Foundgine.Core.Semantic.Planning`
- `src/Foundgine.Runtime/Foundgine.Core.Execution`
- `src/Foundgine.Providers/Foundgine.Providers.Storage.Sql`
- `src/Foundgine.Extensions/Foundgine.Providers.Tools.MCP`

The current release line is **Foundgine 1.2.0**. When this sample is copied outside the repository, the source references should be replaced with `1.2.0` package references (or a later compatible release).
