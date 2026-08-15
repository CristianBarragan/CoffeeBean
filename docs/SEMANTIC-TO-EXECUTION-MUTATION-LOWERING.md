# Semantic → Execution Mutation Lowering

The canonical mutation lowering is:

```text
SemanticMutationOperationGraph
        ↓
SemanticMutationPlanner
        ↓
SemanticMutationPlan
        ↓
SemanticMutationExecutionLowerer   (Planning layer)
        ↓
ExecutionMutationIR
        ↓
Provider compiler
```

The semantic project intentionally does **not** reference the execution project.
Semantic meaning therefore remains independent of execution/runtime assemblies.

The lowering boundary is owned by `Foundgine.Planning` because it is the first
place where semantic `FieldId` values may be resolved to physical `ColumnId`
values through `IMutationSchema`.

Provider-specific decisions remain below `ExecutionMutationIR`:

- SQL syntax
- PostgreSQL batching
- `unnest`
- CTE layout
- transaction implementation
- connection management
- provider-specific conflict syntax

The same semantic plan can therefore be executed through different providers.
