# Getting Started

Foundgine is currently an architecture-first framework under active development. Graphgine is the
first product built on it.

This section explains the repository and the current Banking sample. The sample is a migration
fixture and should not yet be described as a guaranteed one-command production quick start.

## Contents

- [Installation](Installation.md)
- [First Service](First-Service.md)
- [Configuration](Configuration.md)
- [FAQ](FAQ.md)

## Repository layout

```text
src/Foundgine.*       reusable platform
src/Graphgine*        GraphQL product
samples/*             current examples
tests/*               test projects
legacy/*              historical implementation
docs/*                architecture documentation
```

## First validation

With the .NET 9 SDK installed:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln
dotnet test Foundgine.sln
```

The repository should make those commands mandatory CI gates before the project is presented as
production-ready.

## Next

Start with [Architecture](../02-Architecture/README.md) if you want to understand the dependency
boundaries before running code.
