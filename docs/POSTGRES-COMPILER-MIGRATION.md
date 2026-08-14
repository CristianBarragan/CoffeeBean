# PostgreSQL Compiler Migration

The compiler migration should preserve this layering:

```text
ExecutionMutationIR
        ↓
Correlation validation
        ↓
Batch grouping
        ↓
SQL generation
        ↓
SqlBatchedMutationPlan
```

Correlation validation must occur before SQL generation.

Batch grouping must not redefine semantic operation identity.

The existing compiler's `DISTINCT ON`, ordinal mapping, CTE, and `unnest`
mechanisms are implementation details and should be changed only when a
correlation test demonstrates that the current physical strategy cannot satisfy
the semantic contract.
