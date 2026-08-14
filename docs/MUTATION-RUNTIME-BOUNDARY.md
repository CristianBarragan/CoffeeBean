# Mutation Runtime Boundary

The canonical mutation runtime contract is:

```text
Semantic Mutation IR
        ↓
Mutation Planner
        ↓
MutationBatchPlan
        ↓
ExecutionMutationIR
        ↓
IMutationBatchExecutionProvider
        ↓
Provider-specific compilation
        ↓
Provider plan
        ↓
Execution
```

`IMutationBatchExecutionProvider` accepts only `ExecutionMutationIR`.

Provider-plan overloads may remain as implementation details during migration, but
they are not the canonical runtime contract.

The PostgreSQL provider lowers `ExecutionMutationIR` to a batched SQL plan when
possible and falls back to sequential SQL execution when batching is unsupported.
Both paths therefore share the same semantic/execution input.

The provider owns physical lowering. The semantic and execution layers do not
contain SQL, PostgreSQL batching rules, or connection state.
