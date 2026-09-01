# Foundgine source compatibility

The Supply Chain sample is a **source-integrated reference sample**.

All Foundgine dependencies are wired to the repository's `src/` projects with `ProjectReference` entries. This is intentional: the sample must compile against the same implementation that is built and tested by `Foundgine.sln`.

The sample does not use Foundgine `0.5.x` NuGet packages.

Key source dependencies include:

- `src/Foundgine.Abstractions`
- `src/Foundgine.Aot`
- `src/Foundgine.Aot.Generator`
- `src/Foundgine.Metadata`
- `src/Foundgine.Semantics`
- `src/Foundgine.Planning`
- `src/Foundgine.Execution`
- `src/Foundgine.Sql`
- `src/Foundgine.MCP`

The current release line is **Foundgine 1.1.9**. When this sample is copied outside the repository, the source references should be replaced with `1.1.9` package references (or a later compatible release).
