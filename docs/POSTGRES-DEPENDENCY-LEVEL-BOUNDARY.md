# PostgreSQL dependency-level boundary — PostgreSQL Dependency-Level Boundary

The physical PostgreSQL compiler must consume dependency levels derived from the
canonical mutation correlation graph.

Target:

```text
ExecutionMutationIR
        ↓
MutationCorrelationGraph
        ↓
MutationDependencyLevels
        ↓
Postgres batch grouping
        ↓
SQL
```

The PostgreSQL compiler must not independently infer semantic dependency edges
from a second dependency representation.

## Safety rule

If the compiler cannot derive a valid acyclic dependency ordering, compilation
must fail/fallback before SQL generation.

## Current stage

The concrete compiler files were audited before changing the integration.
This stage introduces the documented boundary and preserves compatibility
where the existing baseline has not yet migrated all call sites.
