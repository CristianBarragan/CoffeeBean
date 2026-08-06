[Home](../../README.md) → [Documentation](../README.md) → [Runtime](README.md) → **Execution**

# Execution

## Contents

- [Runtime Pipeline](#runtime-pipeline)
- [Execution Context](#execution-context)
- [Dependency Graph Execution](#dependency-graph-execution)
- [Materialization](#materialization)
- [Transactions](#transactions)
- [Error Handling](#error-handling)
- [Thread Safety](#thread-safety)

---

## Runtime Pipeline

Every request follows the same execution pipeline.

```
Immutable Plan

↓

Execution Context

↓

SQL Generation

↓

Database Execution

↓

Materialization

↓

Return Result
```

The Runtime coordinates each stage but delegates specialized work to other layers.

---

## Execution Context

The execution context carries request-scoped state.

Typical contents include:

- Database connection
- Transaction
- SQL parameters
- Cancellation token
- Execution options

Execution contexts should remain lightweight.

---

## Dependency Graph Execution

Mutations frequently depend on previously generated values.

Example:

```
Customer

↓

CustomerAddress

↓

CustomerOrder
```

The planner computes dependency ordering.

Runtime executes operations in dependency order.

No dependency analysis occurs during execution.

---

## Materialization

Runtime coordinates generated materializers.

```
DbDataReader

↓

Generated Materializer

↓

CLR Object
```

Runtime does not inspect CLR properties.

Generated code performs object construction.

---

## Transactions

Runtime coordinates transaction boundaries.

Typical workflow:

```
Begin Transaction

↓

Execute Plan

↓

Commit

↓

Return Result
```

Failures result in rollback.

Transaction policy remains transport-independent.

---

## Error Handling

Runtime reports execution failures through well-defined exception types.

Typical categories include:

- Validation
- Planning
- SQL
- Materialization
- Transaction
- Graph execution

Transport layers translate these exceptions into protocol-specific responses.

---

## Thread Safety

Runtime services should generally be stateless.

Immutable metadata and immutable execution plans naturally support concurrent execution.

Mutable state should remain confined to execution contexts.

---

---

## Related Documentation

- [Queries](Queries.md)
- [Mutations](Mutations.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)
- [Performance → Benchmarks](../10-Performance/Benchmarks.md)

---

← Previous: [Runtime](README.md)  |  Next: [Queries](Queries.md) →
