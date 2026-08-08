[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Mutations**

# Mutations

## Contents

- [Overview](#overview)

---

> Mutation Planning is responsible for transforming create, update, delete, upsert, connect, disconnect, and graph mutations into a deterministic execution graph. Unlike queries, mutations have ordering constraints, dependencies, transactional semantics, identity propagation, and conflict resolution. The Mutation Planner resolves these concerns before Runtime begins execution.

Runtime executes mutations.

The Mutation Planner understands mutations.

---

## Philosophy

Mutation planning follows one rule:

> **Determine every dependency before execution begins.**

Runtime should never discover ordering.

Runtime should never resolve dependencies.

Everything must already exist in the MutationPlan.

---

## High-Level Pipeline

```
Mutation Request

↓

Planner

↓

Metadata Resolution

↓

Dependency Analysis

↓

Graph Analysis

↓

Ordering

↓

MutationPlan
```

Planning finishes before execution starts.

---

## Why Mutation Planning Exists

Queries are read operations.

Mutations change state.

Changing state introduces additional complexity:

- Ordering
- Transactions
- Identity propagation
- Foreign keys
- Graph dependencies
- Conflict handling

The planner resolves all of these.

---

## Planner Responsibilities

The Mutation Planner is responsible for:

- Entity resolution
- Dependency analysis
- Identity propagation
- Lookup planning
- Upsert planning
- Graph mutation planning
- Conflict analysis
- Execution ordering
- Transaction boundaries

It never executes SQL.

---

## Runtime Responsibilities

Runtime receives a completed MutationPlan.

Runtime performs:

```
MutationPlan

↓

SQL Generation

↓

Execution

↓

Materialization
```

Runtime assumes the plan is valid.

---

## MutationPlan

A MutationPlan is immutable.

Example:

```
MutationPlan

├── Operations

├── Dependencies

├── Graph Operations

├── Lookups

├── Identity References

├── Execution Order

└── Transaction Scope
```

Everything required for execution is already known.

---

## Mutation Operations

Each mutation becomes an operation node.

Examples:

```
Insert

Update

Delete

Upsert

Lookup

Connect

Disconnect
```

Operations become vertices in an execution graph.

---

## Dependency Graph

Mutations naturally form a graph.

Example:

```
Customer

↓

Order

↓

OrderItem
```

OrderItem cannot execute before Order.

Order cannot execute before Customer.

The planner computes this graph.

---

## Dependency Resolution

Dependencies are explicit.

```
Row 0

↓

Row 4

↓

Row 8
```

Runtime never discovers dependency order.

---

## Identity Propagation

Generated identities become dependency references.

Example:

```
Customer.Id

↓

Order.CustomerId
```

Runtime copies values according to the plan.

It never searches for relationships.

---

## Reference Nodes

References are represented explicitly.

```
Reference

Source Row

↓

Target Row

↓

Target Column
```

References remain immutable.

---

## Lookup Planning

Lookups are planned separately.

Example:

```
Country

↓

Lookup

↓

CountryId
```

Runtime receives complete lookup instructions.

---

## Upsert Planning

Upserts require conflict analysis.

Planner determines:

- Conflict columns
- Update columns
- Insert columns
- Identity propagation

Runtime only serializes provider syntax.

---

## Graph Mutation Planning

Graph mutations extend dependency planning.

Example:

```
Customer

↓

Order

↓

OrderItem

↓

Product
```

Traversal order becomes execution order.

---

## Topological Ordering

Execution order is determined through topological sorting.

```
Dependencies

↓

Topological Sort

↓

Execution Sequence
```

Runtime executes sequentially.

---

## Cyclic Detection

Cycles must be detected during planning.

Example:

```
A

↓

B

↓

A
```

Planner reports diagnostics.

Runtime never receives cyclic plans.

---

## Conflict Resolution

Conflict behavior becomes metadata.

Examples:

```
Do Nothing

Update

Replace

Merge
```

Providers translate conflict semantics.

---

## Transaction Planning

Planner determines transactional scope.

```
Entire Mutation

↓

Single Transaction
```

Or

```
Nested Savepoints
```

Runtime coordinates transactions.

---

## Graph Merge Planning

Graph merges become explicit operations.

Example:

```
Customer

↓

CustomerCustomerEdge

↓

Customer
```

Graph operations are independent from SQL generation.

---

## Execution Arms

Independent mutation branches can execute separately.

Example:

```
Customer

↓

Order A

↓

OrderItem A
```

```
Customer

↓

Order B

↓

OrderItem B
```

Planner identifies execution arms.

Future runtimes may parallelize them safely.

---

## Mutation Metadata

Planner consumes:

```
EntityMetadata

MutationMetadata

JoinMetadata

LookupMetadata
```

Runtime never performs metadata analysis.

---

## Alias Allocation

Every mutation node receives a deterministic identifier.

Example:

```
m0

m1

m2

m3
```

Identifiers remain stable.

---

## Parameter Planning

Planner identifies parameter sources.

Examples:

- Literal values
- Generated IDs
- Lookup IDs
- Dependency references

Runtime simply binds values.

---

## Immutable Mutation Graph

Planner builds mutable graphs internally.

Runtime receives immutable graphs.

```
Builder

↓

MutationGraph

↓

MutationPlan
```

Mutation ends before execution begins.

---

## Validation

Planning validates:

- Missing keys
- Invalid references
- Cycles
- Duplicate identities
- Missing lookup values
- Unsupported mutations

Invalid plans are rejected.

---

## Determinism

The same mutation always produces:

- Same node IDs
- Same dependency graph
- Same execution order
- Same SQL structure

Determinism greatly improves testing.

---

## SQL Boundary

Mutation planning ends at:

```
MutationPlan
```

SQL generation begins afterwards.

Providers should never perform dependency analysis.

---

## Runtime Execution

Runtime executes according to the graph.

```
Node

↓

Dependencies Satisfied?

↓

Execute

↓

Propagate Identity

↓

Continue
```

Execution follows the plan exactly.

---

## Materialization

Materialization occurs after execution.

Generated materializers reconstruct:

- Updated entities
- Inserted entities
- Lookup results

No planning occurs.

---

## Testing

Mutation planning should be tested independently.

Recommended tests:

```
Dependency Tests

↓

Identity Tests

↓

Lookup Tests

↓

Topological Order Tests

↓

Snapshot Tests
```

Runtime assumes planner correctness.

---

## Native AOT

Mutation planning naturally supports Native AOT because it relies entirely on generated metadata and immutable models.

No runtime discovery or reflection is required.

---

## Future Evolution

Potential enhancements include:

- Cost-based scheduling
- Parallel execution planning
- Distributed execution
- Generated mutation planners
- Bulk mutation optimization
- Provider-aware planning

Each enhancement should preserve Runtime simplicity.

---

## Mutation Planner Checklist

Before adding mutation logic, ask:

- Is this dependency structural?
- Can it be resolved before execution?
- Is execution order deterministic?
- Is the graph immutable?
- Can Runtime avoid this work?
- Can it be independently tested?

If not, reconsider the design.

---

## Relationship to the Framework

The Mutation Planner forms the boundary between mutation intent and mutation execution.

```
Transport

↓

Mutation Planner

↓

MutationPlan

↓

Runtime

↓

SQL Provider

↓

Database
```

Runtime becomes an execution engine rather than a mutation analyzer.

---

## Summary

The Mutation Planning Architecture transforms mutation requests into immutable dependency graphs by resolving entity relationships, identity propagation, lookup operations, graph traversals, conflict semantics, and execution ordering before Runtime begins.

This design enables deterministic execution, simplified Runtime logic, provider-independent SQL generation, reliable transactional behavior, comprehensive testing, and full Native AOT compatibility while supporting increasingly sophisticated graph mutation scenarios.

---

## Related Documentation

- [Queries](Queries.md)
- [Execution](Execution.md)
- [GraphQL → Schema](../05-GraphQL/Schema.md)

---

← Previous: [Queries](Queries.md)  |  Next: [Events](Events.md) →
