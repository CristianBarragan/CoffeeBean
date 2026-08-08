[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Queries**

# Queries

## Contents

- [Philosophy](#philosophy)
- [High-Level Pipeline](#high-level-pipeline)
- [Why Planning Exists](#why-planning-exists)
- [Planner Responsibilities](#planner-responsibilities)
- [Runtime Responsibilities](#runtime-responsibilities)
- [QueryPlan](#queryplan)

---

## Philosophy

The planner exists for one purpose:

> **Convert intent into instructions.**

A request expresses *what* the client wants.

A QueryPlan describes *how* Runtime will obtain it.

---

## High-Level Pipeline

```
Transport Request

↓

Planner

↓

Metadata Resolution

↓

Relationship Resolution

↓

Projection Analysis

↓

Graph Planning

↓

QueryPlan
```

Planning completes before Runtime begins.

---

## Why Planning Exists

Without planning:

```
Request

↓

Runtime

↓

Analyze Metadata

↓

Build SQL

↓

Execute
```

With planning:

```
Request

↓

Planner

↓

QueryPlan

↓

Runtime

↓

Execute
```

Runtime becomes significantly simpler.

---

## Planner Responsibilities

The planner is responsible for:

- Entity resolution
- Relationship resolution
- Projection analysis
- Join planning
- Graph planning
- Filter normalization
- Ordering
- Pagination
- Aggregation planning
- Alias generation

The planner never executes SQL.

---

## Runtime Responsibilities

Runtime receives a completed plan.

Runtime performs:

```
QueryPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime never revisits planning decisions.

---

## QueryPlan

The QueryPlan is an immutable contract.

Example:

```text
QueryPlan

├── Root Entity
├── Projection
├── Filters
├── Ordering
├── Pagination
├── Graph
├── Joins
└── Result Shape
```

Everything Runtime needs already exists.

---

---

## Related Documentation

- [Execution](Execution.md)
- [Mutations](Mutations.md)
- [GraphQL → Pagination, Filtering & Sorting](../05-GraphQL/Pagination-Filtering-Sorting.md)

---

← Previous: [Execution](Execution.md)  |  Next: [Mutations](Mutations.md) →
