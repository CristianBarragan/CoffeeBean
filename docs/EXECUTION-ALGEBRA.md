# Execution Algebra

The execution plan is Foundgine's logical intermediate representation. It is deliberately smaller than SQL and deliberately independent of GraphQL, EF Core, Dapper, or any other provider.

The central rule is:

> A provider may choose how to execute an operation, but it must not change what the operation means.

## Current algebra

The current implementation has three structural operations:

| Operation | Meaning | Physical interpretation |
|---|---|---|
| `Scan` | Establish the root entity set for the request. | SQL `FROM`, an EF query root, an in-memory collection, or another provider-specific source. |
| `Traverse` | Traverse a declared semantic relationship from the current entity. | Join, lookup, nested collection access, remote call, etc. |
| `TraverseConnection` | Traverse an application-facing semantic connection. | Provider-specific resolution of the connection. |

These operations form a **tree**, preserving request topology and fan-out. They do not contain storage names, SQL fragments, provider objects, expression trees, or delegates.

## Query algebra

Query modifiers are currently represented by `SemanticQueryOptions` attached to the root execution node:

- `Filter`
- `Order`
- `Limit`
- `Offset`
- `After` (forward cursor pagination)

Their meaning is semantic. A provider is responsible for lowering them while preserving their semantics.

This is intentional: query modifiers are not SQL operations. For example, a semantic filter may become a SQL `WHERE`, an in-memory predicate, or a remote API constraint.

## Mutation algebra

Mutations have a separate plan representation because mutation dependency and value-binding semantics are materially different from read traversal.

The minimum mutation vocabulary is:

- `Create`
- `Update`
- `Delete`
- `Upsert`
- `Bind`

Complex mutations must be composition of these primitives plus dependencies and returned values. A new mutation concept must justify why it cannot be represented using this vocabulary.

## Target algebra

The long-term logical vocabulary is expected to converge toward:

```text
Read
Filter
Project
Traverse
Aggregate
Order
Page
Mutate
Bind
Return
```

This is a **directional contract, not a claim that every operation is a first-class `ExecutionPlanNode` today**. Until an operation has a stable semantic contract and provider-independent tests, it must not be added merely to make the enum look complete.

## What the algebra must never contain

The logical plan must not expose:

- SQL text
- table or column names
- SQL aliases
- EF Core query objects
- GraphQL AST nodes
- HTTP requests
- provider-specific plans
- compiled delegates
- database connections

Those belong strictly to adapters or providers.

## Provider conformance

A provider conforms to the algebra when the same logical plan preserves:

1. selected fields;
2. relationship and connection traversal;
3. filtering semantics;
4. ordering semantics;
5. pagination semantics;
6. authorization semantics;
7. result topology.

A provider is free to use a completely different physical strategy.

## Freeze rule

Do not add a new logical operation because a provider needs it.

The correct direction is:

```text
semantic requirement
        ↓
logical operation / modifier
        ↓
provider lowering
```

Never:

```text
provider implementation detail
        ↓
new core operation
```

This keeps the execution plan a genuine intermediate representation rather than a SQL-shaped abstraction with the SQL removed.
