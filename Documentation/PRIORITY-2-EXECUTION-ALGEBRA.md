# P0.2 — Execution Algebra

## Status

**Frozen for the current read model.**

## Contract

The provider-independent read plan is a tree of execution nodes. The structural operation set is exactly:

```text
Scan
Traverse
TraverseConnection
```

A node also carries semantic data:

```text
EntityId
Fields
QueryOptions?
Authorization?
ViaRelationship?
ViaConnection?
Children
```

### Structural meaning

- `Scan` establishes the root entity set.
- `Traverse` follows a resolved semantic relationship.
- `TraverseConnection` follows a pre-resolved semantic connection.

### Clauses are not operations

`Filter`, `Order`, `Limit`, `Offset`, and cursor state are represented by `SemanticQueryOptions`, not by additional `ExecutionOperation` values.

Projection is represented by `Fields`.

Authorization is represented by `Authorization`.

This keeps the logical algebra smaller than SQL and prevents the execution enum from becoming a provider-shaped query AST.

## Current limitation

`SemanticQueryOptions` is currently attached to the root execution node. Nested relationship filters and aggregate predicates can still be represented semantically through the root query. Independent query modifiers on nested result nodes are not part of P0.2 and should not be invented until result/model semantics define their behavior.

## Provider boundary

Providers lower the logical plan into their own physical representation. The logical plan must not contain SQL text, storage names, aliases, GraphQL AST nodes, provider plans, database connections, or compiled provider delegates.

## Freeze rule

A new `ExecutionOperation` is justified only when it represents a fundamentally different kind of logical execution topology and has a provider-independent semantic contract. A provider requirement or an ordinary query modifier is not sufficient justification.

## Validation

The planning tests verify:

1. a root is represented by `Scan`;
2. relationships use `Traverse`;
3. semantic connections use `TraverseConnection`;
4. query clauses do not become execution operations;
5. authorization survives planning;
6. the structural operation set contains exactly the three frozen read operations;
7. the planning project has no provider or transport project dependencies.
