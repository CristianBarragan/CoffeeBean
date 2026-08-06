[Home](../../README.md) → [Documentation](../README.md) → **Runtime**

# Runtime

Runtime is where generated execution plans actually run. It never discovers metadata, parses
attributes, or generates SQL — that all happened at compile time, in
[Source Generators](../06-Source-Generators/README.md). Runtime's job is narrower and more
predictable: execute the plan it was handed.

---

## Contents

- [Execution](Execution.md) — the runtime pipeline, execution context, transactions, error handling
- [Queries](Queries.md) — how the query planner works
- [Mutations](Mutations.md) — how the mutation planner works
- [Events](Events.md) — extension points for observing execution

---

## Philosophy

## Philosophy

The Runtime has one responsibility:

> Execute immutable plans.

It should never discover information.

It should never infer behavior.

It should simply execute.

---

---

## Related Documentation

- [Foundation](../03-Foundation/README.md)
- [GraphQL](../05-GraphQL/README.md)
- [Architecture → Request Pipeline](../02-Architecture/Request-Pipeline.md)

---

← Previous: [Foundation](../03-Foundation/README.md)  |  Next: [GraphQL](../05-GraphQL/README.md) →
