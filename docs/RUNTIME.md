# Runtime

The runtime is split into three steps:

```text
Semantic Graph → Execution Plan → Provider execution
```

## Queries

A request is resolved against the semantic model, authorized, and planned without knowing physical storage.

The SQL provider then translates the logical plan into parameterized SQL.

## Mutations

Mutations use the same separation:

```text
Mutation Intent
 → Mutation Plan
 → Provider mutation plan
 → Execution
 → Result shaping
```

Nested mutations are represented as dependencies rather than hard-coded SQL relationships.

## Results

Execution returns provider-neutral results. Materialization uses the semantic model to rebuild the requested shape.
