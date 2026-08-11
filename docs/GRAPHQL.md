# GraphQL

Hot Chocolate is an adapter, not the Foundgine engine.

```text
GraphQL AST
   ↓
Foundgine GraphQL adapter
   ↓
Semantic Request / Mutation Intent
   ↓
Foundgine runtime
```

## Current support

The test suite covers:

- queries and mutations;
- variables and input coercion;
- named and inline fragments;
- aliases;
- conditional directives;
- multiple operations and operation selection;
- schema/SDL generation;
- structured adapter errors;
- nested mutation result shaping;
- filtering, ordering, pagination, and relationship traversal.

## Boundary

The adapter may understand GraphQL syntax and response shape. It must not add GraphQL concepts to `Foundgine.Semantics`, `Foundgine.Planning`, or the SQL provider.

See `src/Foundgine.GraphQL.HotChocolate` and `src/Foundgine.GraphQL.HotChocolate.Mutations`.
