# Roadmap

## Now

### 1. Productize the semantic → query bridge

Move the proven acceptance-path translation:

```text
ResolvedReadPlan → QueryIntent
```

into the smallest reusable runtime capability possible.

### 2. Make traversal collection-aware

Ensure one-to-many relationships are represented as query traversal rather than forced into single-identity resolution.

Target:

```text
Ada
 ├── Account A → Transactions
 └── Account B → Transactions
```

### 3. Benchmark

Measure the active pipeline before optimizing it:

```text
resolution
read planning
query planning
provider compilation
SQL translation
execution
end-to-end
```

### 4. Simplify semantic mapping

Let existing metadata supply identity, fields, relationships and types where possible. Use semantic configuration for meaning that cannot safely be inferred.

---

## Next

### 5. Domain actions

Expose explicit business operations.

### 6. Policy / authorization

Make authorization part of planning.

### 7. Preview / approval

Make important mutations inspectable before execution.

### 8. Verification / evidence

Verify and explain important executions.

---

## Later

### 9. MCP

Thin adapter over the semantic API.

### 10. Additional execution targets

Structured data, retrieval, external systems and domain actions as adapters.

### 11. Roslyn semantic compiler

Generate semantic vocabulary from application code/metadata where compile-time analysis genuinely removes duplication.

---

## Explicit non-goals

Do not turn the core into:

- an LLM;
- a general agent framework;
- a RAG framework;
- an ORM;
- an MCP implementation;
- a workflow engine;
- a message broker;
- a database.

Those are integration points or neighboring products.
