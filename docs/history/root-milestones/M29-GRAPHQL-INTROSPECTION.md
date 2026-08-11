# M29 — GraphQL Introspection

**Status: IMPLEMENTED**

M29 makes the GraphQL boundary schema-driven and introspection-ready without moving GraphQL schema concepts into Foundgine's semantic, planning, execution, or SQL layers.

## What M29 provides

- `GraphQLSchemaAdapter` builds a deterministic GraphQL schema descriptor from `SemanticModel`.
- Query entity types and relationships are exposed.
- Mutation fields for `create`, `update`, `delete`, and `upsert` are described.
- Input and where-input types are generated from semantic fields.
- CLR scalar types are mapped to GraphQL scalar names.
- `BuildSdl()` produces host-consumable SDL.

## Introspection boundary

Foundgine does **not** implement `__schema` or `__type` execution itself. Those are GraphQL protocol concerns and remain the responsibility of the GraphQL host.

For Hot Chocolate, the generated schema is registered with the host; Hot Chocolate then provides standard GraphQL introspection for clients, IDEs, schema tooling, and code generation.

```text
SemanticModel
      ↓
GraphQLSchemaAdapter
      ↓
GraphQL schema / SDL
      ↓
Hot Chocolate host
      ↓
standard __schema / __type introspection
```

This deliberately avoids adding a GraphQL execution engine to Foundgine.
