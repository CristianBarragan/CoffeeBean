# Foundgine.GraphQL.HotChocolate

Thin Hot Chocolate adapter for query-side GraphQL.

```text
GraphQL → AST → Semantic Request → Foundgine runtime
```

It handles GraphQL syntax, variables, fragments, aliases, directives, operation selection, schema description, and response shape. It does not perform planning, SQL, or execution.
## Install

```bash
dotnet add package Foundgine.GraphQL.HotChocolate
```

## Package scope

This package is a transport adapter. GraphQL is converted into Foundgine semantic requests rather than becoming the semantic model or physical execution layer.

## Repository documentation

- [Current status](https://github.com/CristianBarragan/Foundgine/docs/CURRENT-STATUS.md)
- [Security](https://github.com/CristianBarragan/Foundgine/docs/SECURITY.md)
- [NuGet packaging](https://github.com/CristianBarragan/Foundgine/docs/NUGET-PACKAGING.md)

