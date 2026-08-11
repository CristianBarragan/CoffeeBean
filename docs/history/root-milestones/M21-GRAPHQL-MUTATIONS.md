# M21 — GraphQL Mutations

M21 adds a thin Hot Chocolate adapter for Foundgine mutations.

Supported root operations:

- `createEntity(input: {...})`
- `updateEntity(input: {...}, where: {...})`
- `deleteEntity(where: {...})`
- `upsertEntity(input: {...}, onConflict: [...])`

Nested create input is translated into `NestedMutationIntent`; execution remains in the provider-neutral mutation pipeline.

GraphQL aliases, variables, directives, and multi-root mutations remain unsupported by the M21 adapter.
