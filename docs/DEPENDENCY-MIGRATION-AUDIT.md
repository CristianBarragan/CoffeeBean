# Dependency migration audit — Dependency Migration Audit

The old `MutationDependency` representation is now treated as a compatibility surface rather than semantic truth.

Before deleting it, all source consumers must migrate to:

```text
ExecutionMutationIR
    ↓
correlation references
    ↓
MutationCorrelationGraph
    ↓
dependency levels
```

This stage records the remaining source inventory rather than performing a destructive blind deletion.

## Remaining references

- `tests/Foundgine.Semantics.Tests/SemanticMutationIrTests.cs`
- `tests/Foundgine.E2E.Tests/MutationDependencyTests.cs`
- `tests/Foundgine.E2E.Tests/ExecutionMutationIRTests.cs`
- `src/Foundgine.Sql/Mutation/SqlMutationPlan.cs`
- `src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`
- `src/Foundgine.Sql/Mutation/SqlBatchedMutationPlan.cs`
- `src/Foundgine.Execution/Mutation/DerivedMutationDependencyGraph.cs`
- `src/Foundgine.Execution/Mutation/ExecutionMutationIR.cs`
- `src/Foundgine.Planning/Mutation/MutationDependency.cs`
- `src/Foundgine.Planning/Mutation/SemanticMutationExecutionLowerer.cs`
- `src/Foundgine.Planning/Mutation/MutationBatchPlan.cs`
- `src/Foundgine.Planning/Mutation/MutationPlanner.cs`
- `src/Foundgine.Semantics/Mutation/SemanticMutationBuilder.cs`
- `src/Foundgine.Semantics/Mutation/SemanticMutationOperation.cs`
- `src/Foundgine.Semantics/Mutation/SemanticMutationPlan.cs`
- `src/Foundgine.Semantics/Mutation/SemanticMutationDependency.cs`
- `src/Foundgine.Semantics/Mutation/SemanticMutationPlanner.cs`

## Rule

`MutationDependency` must not be used to introduce a dependency that cannot be derived from a correlation reference.
