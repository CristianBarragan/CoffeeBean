> **Historical note:** This page describes the earlier GraphQL/source-generation architecture. The current Foundgine direction is documented in [Direction](../../00-Direction/README.md) and [Current Status](../../CURRENT-STATUS.md). Historical implementation is under `archive/`.

# Schema

Graphgine is designed to derive GraphQL-facing behavior from domain and mapping metadata rather than
maintaining an unrelated second description of the domain.

## Where metadata comes from

The current architecture is:

```text
Domain / EF Core mappings
        ↓
Graphgine.SourceGenerators
        ↓
generated metadata
        ↓
Graphgine planning/runtime
        ↓
Graphgine.HotChocolate
```

The exact schema-generation surface is still evolving. Do not assume that every generated artifact
described in historical Coffee Beanery documentation exists in the current runtime.

## Graph-shaped metadata

Graphgine models entities, relationships, joins, graph nodes and edges as explicit structures. Those
structures are also used by SQL and graph planning.

See:

- [Foundation → Metadata](../03-Foundation/Metadata.md)
- [Source Generators](../06-Source-Generators/README.md)
- [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md)

## Resolver boundary

Hot Chocolate-facing resolvers belong in the adapter/product layer. Foundgine platform contracts
must remain independent of Hot Chocolate.

See [Resolvers](Resolvers.md).
