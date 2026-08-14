# Foundgine.Semantics

Provider-independent application meaning.

Owns:

- semantic entities and relationships;
- semantic requests and graphs;
- request resolution;
- authorization;
- query controls.

It does not know GraphQL, SQL, or a database.
## Install

```bash
dotnet add package Foundgine.Semantics
```

## Package scope

This package owns application meaning: semantic entities, intent, resolution, capabilities, and authorization. External callers can request capabilities but cannot define them.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

