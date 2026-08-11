# M20 — Mutation Result Materialization

M20 shapes the flat `MutationBatchResult` produced by the provider back into the semantic nested mutation tree.

```text
NestedMutationIntent
        ↓
MutationBatchPlan
        ↓
provider execution
        ↓
MutationBatchResult
        ↓
MutationResultMaterializer
        ↓
MutationMaterializedResult
```

The materializer consumes the original nested intent plus the ordered batch results. Provider-specific SQL, generated-key handling, and parameter bindings do not leak into the result model.

The result tree contains entity identity, returned field values, relationship children, and the operation index used for diagnostics.
