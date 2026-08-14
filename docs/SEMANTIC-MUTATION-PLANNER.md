# Semantic Mutation Planner

The semantic mutation planner is now an explicit boundary:

```text
SemanticMutationOperationGraph
        ↓
SemanticMutationPlanner
        ↓
SemanticMutationPlan
  ├── operations
  └── dependencies
        ↓
ExecutionMutationIR
        ↓
Provider
```

The planner does not emit SQL or provider plans.

The existing provider-oriented `MutationBatchPlan` remains available only as a migration surface in this stage. It should be lowered from `SemanticMutationPlan`, rather than being the semantic planner's canonical output.

## Architectural invariant

The mutation planner answers:

> What semantic operations, effects, and dependencies constitute this mutation?

The execution lowering answers:

> What physical work must a provider perform to realize that semantic plan?

This keeps batching, CTEs, `unnest`, conflict handling, transaction strategy, and provider-specific optimizations outside semantic planning.


## Dependency is the single semantic value-flow primitive

A dependency from source operation/field to target operation/field already means
that the produced value must remain associated with its logical source while it
crosses the operation boundary. A second `Correlation` edge collection therefore
added no semantic information; it duplicated the same source, target, and field
identity.

The semantic planner now exposes exactly one dependency collection. Physical
correlation is introduced only during execution/provider lowering. PostgreSQL may
use a compiler-owned `__fg_corr` carrier, `WITH ORDINALITY`, and `RETURNING`; those
are implementation mechanisms rather than semantic graph concepts. PostgreSQL 17
allows `MERGE RETURNING` to reference both source and target rows, which is one
possible physical realization of this dependency contract.

