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

The current read contract carries `SemanticQueryOptions` on the **root execution node**. The current options are:

- `Filter`
- `Order`
- `Limit`
- `Offset`
- `After` (forward cursor pagination)

Their meaning is semantic. A provider is responsible for lowering them while preserving their semantics.

This is an explicit current limitation, not an accidental omission: the execution algebra does **not** yet model independent query clauses on nested result nodes. Nested relationship filtering and aggregation can still be expressed through semantic filter expressions on the root query. Per-child pagination/ordering/filtering is deferred until the result/model semantics are defined.

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

## What is frozen in P0.2

For the current read model, the structural execution algebra is exactly:

```text
Scan
Traverse
TraverseConnection
```

`Fields`, `SemanticQueryOptions`, and `Authorization` are node data/clauses rather than execution operations. Mutations remain a separate planning algebra.

There is deliberately **no speculative target enum** in the current contract. Concepts such as projection, aggregation, ordering, and pagination will only become new logical constructs if a future semantic requirement proves that the existing node/clauses cannot express them cleanly.

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

## P0.2 freeze rule

Do not add a new logical operation because a provider needs it. Do not add one merely because a query feature exists.

A new structural operation is justified only when it represents a fundamentally different kind of logical execution topology and can be specified independently of any provider.

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
