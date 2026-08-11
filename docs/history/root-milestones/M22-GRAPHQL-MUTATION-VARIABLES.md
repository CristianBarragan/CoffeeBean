# M22 — GraphQL Mutation Variables

M22 adds runtime GraphQL variable support to the Hot Chocolate mutation adapter.

The adapter now accepts:

```csharp
adapter.Adapt(graphql, variables);
```

For example:

```graphql
mutation CreateCustomer($input: CustomerInput!) {
  createCustomer(input: $input) {
    id
    name
  }
}
```

with:

```json
{
  "input": {
    "name": "Ada"
  }
}
```

produces the same provider-neutral `MutationIntent` as the equivalent inline
mutation:

```graphql
mutation {
  createCustomer(input: { name: "Ada" }) {
    id
    name
  }
}
```

## Boundary

Variable resolution is deliberately confined to `Foundgine.GraphQL.HotChocolate`.
The semantic, planning, execution, and SQL layers do not know that a value came
from a GraphQL variable.

```text
GraphQL document + runtime variables
                │
                ▼
HotChocolateMutationAdapter
                │
                ▼
        MutationIntent
                │
                ▼
       existing pipeline
```

## Supported variable locations

- mutation `input`
- mutation `where`
- mutation `onConflict` / `conflict`
- nested mutation input
- nested lists and object values
- scalar variables
- GraphQL variable default values

Runtime dictionaries, lists, and `JsonElement` values are normalized at the
adapter boundary before becoming mutation values.

## Deliberate scope

M22 does not introduce a variable-reference type into Foundgine planning or
execution. It also does not add GraphQL directives, aliases, fragments, or
multi-root mutations.

The existing `Adapt(string graphql)` overload remains available for inline
values and delegates to the variable-aware overload.
