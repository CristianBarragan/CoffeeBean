# Semantic Plan Boundary

`SemanticPlan` is the canonical output of the provider-independent planner.

The planning boundary is:

```text
Semantic IR
    ↓
Planner
    ↓
SemanticPlan
    ↓
ExecutionIRCompiler
    ↓
ExecutionIR
```

`SemanticPlan` is a planning artifact. It does not represent a provider request and
must not contain SQL, storage names, aliases, connections, or provider types.

`ExecutionIR` is the only canonical input to provider compilation.

`ExecutionPlan` remains temporarily as a compatibility adapter for the pre-IR API.
New code must not use it as an execution abstraction.

The migration is complete when:

- `IPlanner` returns `SemanticPlan`
- providers consume `ExecutionIR`
- provider compilers no longer expose `Compile(ExecutionPlan)`
- runtime/result materialization consume `SemanticPlan` or `ExecutionIR` explicitly
- `ExecutionPlan` can be removed
