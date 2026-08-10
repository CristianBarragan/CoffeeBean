# Runtime

The runtime turns structured intent into executable provider plans.

```text
QueryIntent
   ↓
QueryPlanner
   ↓
QueryPlan
   ↓
SqlPlanCompiler
   ↓
ProviderPlan
   ↓
SqlExecutionProvider
```

The runtime also contains the lower-level mutation planning path.

## Semantic bridge

The semantic layer is intentionally separate from the runtime planner.

The required bridge is:

```text
ResolvedReadPlan
       ↓
QueryIntent
       ↓
QueryPlanner
```

The current acceptance tests prove this connection end to end, but the translation is not yet a dedicated reusable public runtime component.

The bridge must remain small. It should not become a second planner or an AI orchestration layer.

## Current focus

The next runtime work is:

1. productize the semantic → query translation;
2. make collection-valued traversal explicit;
3. benchmark the complete pipeline.
