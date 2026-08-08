[Home](../../README.md) → [Documentation](../README.md) → [Persistence](README.md) → **Dapper & EF Core**

# Dapper & EF Core

## Contents

- [Two different jobs](#two-different-jobs)
- [EF Core: metadata source](#ef-core-metadata-source)
- [Dapper: execution](#dapper-execution)
- [Query & Mutation Generation](#query--mutation-generation)

---

## Two different jobs

It's easy to assume Coffee Beanery is "an EF Core + Dapper hybrid ORM." It's more precise to
say: **EF Core supplies metadata, Dapper executes.** They're not layered or composed at
runtime — EF Core's mapping classes are read by the
[mapping generator](../06-Source-Generators/Mapping-Generator.md) at compile time, and by
request time, EF Core isn't in the path at all.

## EF Core: metadata source

Mapping classes (`BaseModelMappingRegistration<T>`, `BuildMap()`) describe the relationship
between your domain model and your EF Core entity model. The generator parses that
description at compile time — see
[Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md#required-changes-to-existing-hand-written-code)
for the exact shape it expects.

## Dapper: execution

At request time, generated SQL executes through `Dapper.Contrib` and `Z.Dapper.Plus` (for
bulk upserts) — no `DbContext`, no EF Core change tracking, no reflection-based materialization.
Rows come back through `Mapper.MapByAlias`, using pre-compiled getter/setter delegates built
during [warmup](Caching.md). See
[Performance → Benchmarks](../10-Performance/Benchmarks.md#why-response-times-are-this-low)
for the concrete mechanism.

## Query & Mutation Generation

## Query Generation

Typical pipeline:

```
Projection

↓

FROM

↓

JOIN

↓

WHERE

↓

GROUP BY

↓

ORDER BY

↓

LIMIT

↓

OFFSET
```

Each clause should be generated independently.

---

## Mutation Generation

Mutation generation typically consists of:

```
INSERT

↓

ON CONFLICT

↓

DO UPDATE

↓

RETURNING
```

or

```
WITH

↓

Dependency CTEs

↓

INSERT

↓

RETURNING
```

Dependency ordering is supplied by the Runtime.

---

---

## Related Documentation

- [PostgreSQL & AGE](PostgreSQL-AGE.md)
- [Source Generators → Mapping Generator](../06-Source-Generators/Mapping-Generator.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [PostgreSQL & AGE](PostgreSQL-AGE.md)  |  Next: [Caching](Caching.md) →
