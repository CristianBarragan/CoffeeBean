# Semantic IR

Semantic IR is the canonical representation of a resolved operation between
semantic resolution and provider-independent planning.

## Boundary

```text
Intent
  ↓
Resolution
  ↓
Semantic Graph
  ↓
Semantic IR
  ↓
Planner
  ↓
Execution IR
  ↓
Provider
```

Semantic IR contains semantic identities and constraints only.

It must not contain:

- SQL
- table or column storage names
- provider-specific nodes
- GraphQL AST nodes
- database connections
- executable provider delegates

The current implementation introduces `SemanticOperation` and
`SemanticReadNode`, plus `SemanticOperationCompiler` which performs a
loss-preserving lowering from `SemanticGraph`.

This is deliberately additive in the first migration stage. Existing
planning and provider paths remain unchanged so the new boundary can be
validated before downstream consumers are migrated.

## Migration rule

The next planning step should consume `SemanticOperation` directly. The
existing `SemanticGraph -> ExecutionPlan` path remains temporarily available
for compatibility during migration.


## Planner boundary

The planner consumes `SemanticOperation` as its canonical input:

```text
SemanticGraph
    ↓
SemanticOperationCompiler
    ↓
SemanticOperation
    ↓
Planner
    ↓
ExecutionPlan
```

`Planner.Plan(SemanticGraph)` remains only as a compatibility adapter. Application orchestration should compile semantic graphs before planning. This prevents the planner from interpreting graph structure and makes the Semantic IR boundary explicit.
