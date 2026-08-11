# M23 — GraphQL Fragments

M23 adds named GraphQL fragment expansion at the Hot Chocolate adapter boundary.

Supported:

- named fragments in query selections;
- named fragments in mutation result selections;
- nested fragment composition;
- fragment type-condition validation;
- fragment cycle detection;
- existing inline fragments remain supported.

The expansion happens before Foundgine semantic objects are produced. The core
therefore never sees `FragmentSpreadNode`, fragment names, or other GraphQL
syntax.

Example:

```graphql
mutation CreateCustomer($input: CustomerInput!) {
  createCustomer(input: $input) {
    ...CustomerFields
  }
}

fragment CustomerFields on Customer {
  id
  name
}
```

is translated to the same provider-neutral mutation result shape as an inline
`id`/`name` selection.

Deferred:

- fragment directives;
- aliases;
- type-specific polymorphic fragment semantics.
