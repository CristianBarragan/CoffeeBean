# Resolvers

> **Historical / future adapter documentation.**

The active Foundgine core does not expose a GraphQL schema or resolver layer.

The corresponding historical implementation is under `archive/`.

If GraphQL is reintroduced, it should be an adapter that converts GraphQL selections/arguments into Foundgine structured intent. It must not move GraphQL concepts into:

- `Foundgine.Metadata`;
- `Foundgine.Semantic`;
- `Foundgine.Planning`;
- `Foundgine.Execution.Contracts`.

The desired boundary is:

```text
GraphQL request
      ↓
GraphQL adapter
      ↓
Foundgine structured intent
      ↓
Foundgine planner
```
