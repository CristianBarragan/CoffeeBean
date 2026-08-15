# Execution Mutation IR

`ExecutionMutationIR` is the canonical provider-neutral execution representation
for mutation batches.

The mutation pipeline is:

```text
Semantic Mutation IR
        ↓
MutationPlanner
        ↓
MutationBatchPlan
        ↓
ExecutionMutationIR
        ↓
Provider compiler
        ↓
Provider plan
        ↓
Runtime
```

The semantic layer defines what the mutation means. The mutation planner lowers
semantic identities into provider-neutral execution requirements. The execution
IR is the contract consumed by physical providers.

`ExecutionMutationIR` may contain metadata-level identities such as
`ColumnId`, because those are required to describe concrete provider-neutral
work. It contains no SQL, connection, transaction, PostgreSQL CTE, `unnest`,
or provider-plan representation.

The old `MutationBatchPlan` overloads remain temporarily as compatibility
surfaces. New execution paths should use `ExecutionMutationIR`.

A provider may internally adapt the execution IR to an existing compiler during
migration, but the public boundary is the execution IR.
