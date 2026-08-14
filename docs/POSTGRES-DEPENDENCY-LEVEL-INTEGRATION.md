# PostgreSQL dependency-level integration — PostgreSQL Dependency-Level Integration

The PostgreSQL physical boundary now has an explicit representation for
consuming dependency levels:

```text
ExecutionMutationIR
        ↓
MutationCorrelationGraph
        ↓
MutationExecutionLevels
        ↓
PostgresMutationBatchBoundary
        ↓
PostgresBatchedMutationCompiler
```

The boundary deliberately does not let PostgreSQL discover semantic
dependencies.

## Compiler audit

The concrete compiler files in the uploaded baseline were located before
making the integration change:

- `src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`

The next migration should wire the concrete compiler to the boundary using its
existing API rather than inventing a second dependency algorithm.

## Physical compiler responsibilities

Once dependency levels are supplied, PostgreSQL may decide:

- which operations can share a physical batch;
- SQL/CTE layout;
- `unnest` strategy;
- `RETURNING` projection;
- conflict handling;
- transaction strategy.

It may not change the dependency ordering or logical operation identity.
