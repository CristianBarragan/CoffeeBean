# Execution IR

Foundgine now distinguishes the planner's semantic execution plan from the
canonical representation consumed by execution.

```text
Semantic IR
    ↓
Planner
    ↓
ExecutionPlan
    ↓
ExecutionIRCompiler
    ↓
Execution IR
    ↓
Provider compiler
    ↓
Provider plan
```

`ExecutionIR` is provider-neutral. It may describe execution operations,
entities, fields, traversal topology, query controls and authorization
constraints, but it must not contain SQL, table names, column names, provider
types, aliases, connections or other physical storage details.

The current provider boundary still exposes the legacy `ExecutionPlan`
contract. `IProviderPlanCompiler.Compile(ExecutionIR)` therefore has a
compatibility implementation that projects the IR back to the legacy plan.

That adapter is temporary. The next migration step is to make SQL and
InMemory provider compilers consume `ExecutionIR` directly and remove the
reverse projection from the execution path.

The important invariant is:

> Execution IR is the only provider-neutral representation handed from
> planning into provider compilation.

This makes the physical lowering boundary explicit and testable without
requiring a provider rewrite.
