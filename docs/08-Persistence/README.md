# Persistence

Persistence is an execution target, not the semantic model.

## Current proof

```text
QueryPlan
   ↓
SqlPlanCompiler
   ↓
ProviderPlan
   ↓
SqlExecutionProvider
   ↓
SQLite
```

SQLite is used because it makes the E2E proof self-contained.

## Provider direction

The architecture is intended to allow additional providers later.

That does not mean every provider is implemented today.
