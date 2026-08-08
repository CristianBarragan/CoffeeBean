# GraphQL / Graphgine

Graphgine is the first product built on Foundgine. It provides GraphQL-oriented planning and
execution structures while keeping the reusable Foundgine layers independent of GraphQL.

## Boundary

```text
Hot Chocolate
     ↓
Graphgine.HotChocolate
     ↓
Graphgine
     ↓
Foundgine
```

Hot Chocolate dependencies belong in `Graphgine.HotChocolate`, not in Foundgine platform projects.

## Current areas

Graphgine contains:

- selection IR
- query planning
- mutation planning
- filtering
- ordering
- pagination
- PostgreSQL SQL generation
- graph/AGE structures
- mapping metadata integration

## Status

These are real implementation areas, but the product is still under active development. Some graph,
provider and end-to-end execution paths remain incomplete.

## Related

- [Schema](Schema.md)
- [Resolvers](Resolvers.md)
- [Paging, Filtering & Sorting](Pagination-Filtering-Sorting.md)
- [Architecture](../02-Architecture/README.md)
