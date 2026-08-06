[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Schema**

# Schema

## Contents

- [Where the schema comes from](#where-the-schema-comes-from)
- [Node metadata](#node-metadata)
- [Wrapper resolvers](#wrapper-resolvers)

---

## Where the schema comes from

The GraphQL schema is composed from the same EF Core mapping classes that drive the rest of
Coffee Beanery — there's no separate schema-first `.graphql` file to keep in sync. Each
mapping class (a `BaseModelMappingRegistration<T>`) contributes a node to the schema, built
from the `NodeMap` / `NodeTree` structures under `GraphQL/Core/GraphQL` and
`GraphQL/Core/Mapping` in the runtime project. See
[Foundation → Metadata](../03-Foundation/Metadata.md) for the underlying metadata shapes and
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md) for how
those mapping classes are compiled into that metadata.

## Node metadata

At the framework level, the schema is a graph of nodes and edges (`NodeTree`, `Edge`,
`GraphMap`, `LinkKey` — see `GraphQL/Core/Sql`), which is also what powers the graph-shaped
read path over PostgreSQL + Apache AGE described in
[Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md).

## Wrapper resolvers

The sample exposes queries and mutations through thin wrapper resolvers —
`WrapperQueryResolver` and `WrapperMutationResolver` in `Api/Api.Banking` — that delegate
into the runtime rather than hand-writing per-field resolution logic. See
[Resolvers](Resolvers.md) for how that handoff works.

---

## Related Documentation

- [Resolvers](Resolvers.md)
- [Foundation → Metadata](../03-Foundation/Metadata.md)
- [Getting Started → First Service](../01-Getting-Started/First-Service.md)

---

← Previous: [GraphQL](README.md)  |  Next: [Resolvers](Resolvers.md) →
