# PostgreSQL dependency consumer audit — PostgreSQL Dependency Consumer Audit

The PostgreSQL mutation path must consume the canonical correlation graph.

Required path:

```text
ExecutionMutationIR
        ↓
MutationCorrelationReference
        ↓
MutationCorrelationGraph
        ↓
DAG levels
        ↓
Postgres batch grouping
```

The legacy `MutationDependency` type may only appear behind the compatibility
adapter while migration is in progress.

This stage records the remaining source consumers before their individual
migration. No blind global replacement is performed because dependency
construction and execution semantics differ between callers.
