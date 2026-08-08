[Home](../../README.md) → [Documentation](../README.md) → [GraphQL](README.md) → **Resolvers**

# Resolvers

## Contents

- [The wrapper pattern](#the-wrapper-pattern)
- [Query handling](#query-handling)
- [Service layer](#service-layer)

---

## The wrapper pattern

Rather than one Hot Chocolate resolver method per field, Coffee Beanery routes GraphQL
requests through a small number of wrapper resolvers (`WrapperQueryResolver`,
`WrapperMutationResolver`) that parse the incoming field selection and hand it to the
runtime's [query planner](../04-Runtime/Queries.md) or
[mutation planner](../04-Runtime/Mutations.md) as a whole. This is what makes a single
GraphQL query resolve through one batched SQL statement instead of one query per field/edge.

## Query handling

The runtime's `Service` layer — `ProcessService`, `ProcessQuery`, `QueryHandler`,
`QueryResult` — sits between the GraphQL wrapper resolver and the SQL/Dapper execution
layer. It receives the parsed request, invokes the generated planner, and returns a
`QueryResult` the resolver serializes back to the client.

## Service layer

```
GraphQL Resolver (WrapperQueryResolver / WrapperMutationResolver)
        │
        ▼
ProcessService → ProcessQuery / QueryHandler
        │
        ▼
Runtime Query/Mutation Planner  (see Runtime → Execution)
        │
        ▼
SQL generation + Dapper execution  (see Persistence)
```

See [Runtime → Execution](../04-Runtime/Execution.md) for what happens once the planner has
control.

---

## Related Documentation

- [Schema](Schema.md)
- [Runtime → Execution](../04-Runtime/Execution.md)
- [Persistence](../08-Persistence/README.md)

---

← Previous: [Schema](Schema.md)  |  Next: [Pagination, Filtering & Sorting](Pagination-Filtering-Sorting.md) →
