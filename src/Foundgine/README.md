# Foundgine

The main Foundgine facade for semantic execution in .NET.

```bash
dotnet add package Foundgine
```

Use this package when application code needs the main Foundgine service/facade. Foundgine coordinates semantic intent, authorization, provider-independent planning, execution, and execution evidence.

## Boundary

The package is part of the Foundgine semantic execution pipeline:

```text
structured intent
    ↓
semantic model + resolution
    ↓
authorization
    ↓
provider-independent plan
    ↓
physical provider / adapter
```

It does not turn the caller into the authority for what the application exposes or what the physical provider may execute.

## Documentation

- [Getting Started](https://github.com/CristianBarragan/Foundgine/docs/GETTING-STARTED.md)
- [Public Api](https://github.com/CristianBarragan/Foundgine/docs/PUBLIC-API.md)
- [Architecture](https://github.com/CristianBarragan/Foundgine/docs/ARCHITECTURE.md)

## Project status

Foundgine 0.1.x is an early public release. The repository documents the currently proven scope and does not claim universal provider support or independent security certification.

- [Repository](https://github.com/CristianBarragan/Foundgine)
- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
