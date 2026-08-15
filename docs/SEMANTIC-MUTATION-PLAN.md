# Semantic Mutation Plan

`SemanticMutationPlan` is the canonical planning artifact between Semantic Mutation IR and Execution Mutation IR.

```text
Semantic Mutation IR
        ↓
SemanticMutationPlan
        ↓
ExecutionMutationIR
        ↓
Provider
```

The plan describes semantic operations, effects, and dependencies.

It must not contain:

- SQL
- table names
- column aliases
- PostgreSQL constructs
- provider plan types
- connection state

Physical lowering occurs only when the plan is converted to `ExecutionMutationIR`.

This preserves the invariant that batching, CTEs, `unnest`, transactions, and provider-specific execution strategies remain implementation details of the execution/provider layer.
