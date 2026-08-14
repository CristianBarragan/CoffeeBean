# Mutation Migration Status

Canonical target:

```text
SemanticMutationOperationGraph
        ↓
SemanticMutationPlan
        ↓
SemanticMutationExecutionLowerer
        ↓
ExecutionMutationIR
        ↓
Provider
```

Legacy path still present elsewhere:

```text
MutationBatchPlan
```

It must not become the source of semantic truth.

The next migration should update the PostgreSQL and other provider entry points
to receive the lowered `ExecutionMutationIR` directly, then remove the
`MutationBatchPlan` compatibility path.
