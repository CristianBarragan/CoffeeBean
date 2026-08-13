# P1 — Mutation Simplification

Mutation planning already has a small provider-neutral vocabulary:

- `Create`
- `Update`
- `Delete`
- `Upsert`
- dependency references between earlier and later operations

The simplification in this priority is intentionally **internal**. It does not remove supported mutation capabilities or introduce a new mutation algebra.

## What was simplified

`MutationPlanner` previously implemented the same two pieces of logic independently for ordinary batches and nested mutation trees:

1. convert an `IMutationIntent` into one `MutationOperation`;
2. inspect field references and build `MutationDependency` records.

Those paths now share two private helpers:

- `PlanSingle(...)`
- `BuildDependencies(...)`

The public intent types and provider-neutral plan types remain unchanged.

## Why this is the right level of simplification

The mutation planner should describe **what must happen and in what dependency order**. It should not describe SQL CTEs, PostgreSQL `RETURNING`, conflict syntax, or transaction mechanics.

The resulting boundary remains:

```text
MutationIntent / NestedMutationIntent
        ↓
MutationPlanner
        ↓
MutationPlan / MutationBatchPlan
        ↓
Provider compiler
        ↓
SQL / other physical execution
```

## Explicitly not changed

This priority does not:

- merge mutations into the read `ExecutionOperation` enum;
- remove `UpsertIntent`;
- expose SQL concepts in `Foundgine.Planning`;
- redesign nested mutation semantics;
- change provider execution behavior;
- change the public mutation API.

Those would be separate architectural decisions and are not justified by the current evidence.

## Invariant

Batch mutations and nested mutations must produce dependencies using the same validation rules:

- source operation must exist;
- source operation must precede the target;
- referenced source field must be returned;
- dependency records identify source operation, target operation, source field, and target column.
