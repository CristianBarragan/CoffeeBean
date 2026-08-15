> Source content for [`index.html`](index.html), the page actually served on the site. Edit this file, then regenerate the HTML page and `llms-full.md`.

# Foundgine Architecture

## Core execution pipeline

```text
Intent
  ↓
Semantic Model
  ↓
Resolution
  ↓
Authorization
  ↓
Plan
  ↓
Rewrite / Optimize
  ↓
Provider Compilation
  ↓
Execution
  ↓
Result + Evidence
```

## Separation of concerns

### Semantic model

Defines the application-facing capabilities.

### Intent

Describes what the caller wants without binding it directly to a physical provider.

### Authorization

Determines which parts of the requested operation are permitted and can contribute predicates or constraints to the execution plan.

### Planner

Builds a provider-independent representation of the requested operation.

### Rewriting and optimization

Transforms the plan while preserving semantics and authorization constraints.

### Provider

Compiles and executes the plan against a concrete backend.

## Multiple callers, one execution model

```text
REST/API
GraphQL
JSON
AI Agent
Automation
   │
   ▼
Semantic Intent
   │
   ▼
Foundgine Planner
   │
   ├── SQL
   ├── InMemory
   └── Future providers
```

The goal is to avoid implementing separate execution semantics for every interface.

## Why the intermediate plan matters

The plan is the architectural boundary between semantic intent and physical execution.

It gives the runtime a place to:

- preserve authorization constraints
- validate dependencies
- rewrite operations
- estimate cost
- reason about provider capabilities
- optimize execution
- produce execution evidence

This is the core mechanism that allows multiple input surfaces and providers to share execution semantics.

## What's new in 0.3.0

Foundgine 0.3.0 expands the validated semantic execution surface across querying, mutations, authorization, execution, and integrations.

### Richer query semantics
- Relationship filtering and ordering
- Cursor and compound-cursor pagination
- Aggregates and aggregate filtering
- Nested collection traversal

### Mutation execution
- Nested mutations
- Upsert workflows
- Mutation dependency graphs
- Mutation → Query → Mutation → Query workflows
- Structured mutation results

### Execution safety and control
- Authorization-aware planning and execution
- Plan approval
- Context-safe plan caching
- Execution IR and execution receipts
- Untrusted/adversarial intent safety

### Integration and deployment
- GraphQL queries and mutations
- JSON structured intent
- MCP integration
- AI-agent semantic boundary
- Native AOT metadata and SQL pipeline
- SQL, PostgreSQL, and InMemory execution

