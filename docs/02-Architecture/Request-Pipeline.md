# Request Pipeline

## Current proven read pipeline

```text
Structured ReadIntent
        ↓
EntityResolver / ReadPlanner
        ↓
ResolvedReadPlan
        ↓
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
        ↓
SQLite
        ↓
ExecutionRow
```

The current acceptance path proves the complete chain against a real database. The `ResolvedReadPlan → QueryIntent` translation is still assembled in the acceptance path and is therefore the next reusable runtime component to extract.

## Resolution is not traversal

This distinction is central to the architecture.

Resolution answers:

> **Which concrete entity does this reference identify?**

Example:

```text
"Ada Lovelace"
      ↓
Customer #1
```

Traversal answers:

> **Which related set should the query walk?**

Example:

```text
Customer #1
   ↓ 1:N
Accounts
   ↓ 1:N
Transactions
```

Therefore a collection-valued relationship should normally become part of `QueryIntent`, not be forced through single-identity resolution.

## Structured intent comes first

Foundgine should not depend on how intent was produced.

The input may come from:

```text
LLM
UI
REST
MCP
application code
test
```

All should eventually converge on the same structured semantic contract.

## Query planning

The semantic layer should translate into the existing `QueryIntent` model.

There should be one logical planner:

```text
Semantic intent
      ↓
QueryIntent
      ↓
QueryPlanner
```

Do not introduce a parallel `SemanticPlanner → QueryPlanner → ExecutionPlanner` hierarchy.

## Provider execution

The provider decides how to realize the plan.

For SQL:

```text
QueryPlan
 → ProviderPlan
 → SQL
```

The logical planner does not emit SQL.
