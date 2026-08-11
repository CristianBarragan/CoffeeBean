# M25 — GraphQL Directives

M25 adds support for the standard GraphQL conditional directives:

- `@include(if: Boolean!)`
- `@skip(if: Boolean!)`

Directive evaluation is deliberately confined to `Foundgine.GraphQL.HotChocolate`.
No directive or GraphQL-variable concepts are added to Foundgine's provider-neutral semantic, planning, mutation, or SQL layers.

## Supported locations

Conditional directives are supported on:

- query fields
- inline fragments
- fragment spreads
- fragment definitions
- mutation result fields
- mutation result inline fragments
- mutation result fragment spreads

The adapter resolves directive conditions before creating provider-neutral selections or result projections.

## Variables

Both literal and variable conditions are supported:

```graphql
query Customer($withEmail: Boolean!) {
  customer {
    id
    email @include(if: $withEmail)
  }
}
```

Defaults are also supported:

```graphql
query Customer($withEmail: Boolean! = false) {
  customer {
    id
    email @include(if: $withEmail)
  }
}
```

## Scope

M25 intentionally does not add arbitrary custom directive execution. Unsupported directives fail explicitly at the GraphQL adapter boundary.

Root operation directives are also rejected because the current provider-neutral request model has no representation for a conditionally absent root operation.
