# Foundgine.Aot

Compile-time metadata contracts and attributes used by the optional Roslyn generator.

The generated provider feeds the normal runtime metadata boundary. It does not generate SQL, GraphQL, or execution plans.
## Install

```bash
dotnet add package Foundgine.Aot
```

## Package scope

The package contains the runtime AOT attributes/support. The Roslyn generator is packaged inside this package under `analyzers/dotnet/cs/`, and `Foundgine.Metadata.dll` is bundled for runtime use.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

