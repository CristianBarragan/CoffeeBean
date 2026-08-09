> **Historical note:** This page describes the earlier GraphQL/source-generation architecture. The current Foundgine direction is documented in [Direction](../../00-Direction/README.md) and [Current Status](../../CURRENT-STATUS.md). Historical implementation is under `archive/`.

[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Pagination, Filtering & Sorting**

# Pagination, Filtering & Sorting

## Contents

- [Where it's implemented](#where-its-implemented)
- [Compile-time vs. runtime](#compile-time-vs-runtime)

---

## Where it's implemented

Paging, filtering, and ordering are implemented as first-class parts of the runtime, not as
Hot Chocolate middleware layered on top of an `IQueryable`:

- `GraphQL/Core/Runtime/Paging` — cursor-based pagination
- `GraphQL/Core/Runtime/Filtering` — filter construction
- `GraphQL/Core/Runtime/Ordering` — sort construction

These feed directly into the SQL query compiler (`SqlPagingCompiler`, `SqlWhereCompiler`,
`SqlOrderCompiler` in `GraphQL/Core/Runtime`) described in
[Persistence](../08-Persistence/README.md), which means a filtered, sorted, paginated query
is still resolved as a single generated SQL statement rather than an in-memory filter over a
fully materialized result set.

## Compile-time vs. runtime

The *shape* of what's filterable/sortable per field comes from compile-time metadata (see
[Foundation → Metadata](../03-Foundation/Metadata.md)); the specific filter/sort *values* in
a given request are naturally resolved at runtime, but without any reflection-based property
lookup — see [Performance → Benchmarks](../10-Performance/Benchmarks.md) for how the
mapping layer avoids that cost.

---

## Related Documentation

- [Persistence → PostgreSQL & AGE](../08-Persistence/PostgreSQL-AGE.md)
- [Runtime → Queries](../04-Runtime/Queries.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Resolvers](Resolvers.md)  |  Next: [Source Generators](../06-Source-Generators/README.md) →
