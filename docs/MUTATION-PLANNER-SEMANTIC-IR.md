# Mutation Planner — Semantic IR Boundary

The canonical mutation planning entry point is:

```text
SemanticMutationOperationGraph
        ↓
MutationPlanner
        ↓
MutationBatchPlan
        ↓
Execution/lowering
```

`MutationPlanner` is responsible for validating semantic legality and lowering
semantic field identities to the existing provider-neutral mutation planning
contracts. It does not emit SQL or provider plans.

Semantic field identities are mapped to physical `ColumnId` values only at this
planning boundary, using `IMutationSchema`. Provider-specific mutation compilers
remain downstream consumers.

Dependencies originate from semantic value references and explicit semantic
dependency declarations. They are lowered to provider-neutral dependency edges.

The existing `Plan(MutationIntent...)` APIs remain for compatibility while callers
migrate to `Plan(SemanticMutationOperationGraph)`.
