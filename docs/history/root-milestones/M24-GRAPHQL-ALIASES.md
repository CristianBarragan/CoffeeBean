# M24 — GraphQL Aliases

## Goal

Support GraphQL aliases without contaminating Foundgine's provider-neutral semantic and mutation contracts.

For example:

```graphql
query {
  customer {
    customerId: id
    displayName: name
  }
}
```

The engine still sees `Id` and `Name` as their normal semantic fields. The GraphQL adapter additionally exposes:

- GraphQL field name
- response alias
- nested result projections

## API

Use:

```csharp
var adaptation = adapter.AdaptResultShape(graphql);
```

for query result projections, or:

```csharp
var adaptation = adapter.AdaptResultShape(graphql, variables);
```

for mutation result projections.

`Adapt(...)` remains available when only the provider-neutral intent/request is required.

## Architectural rule

Aliases are response syntax, not domain semantics.

They therefore stay in `Foundgine.GraphQL.HotChocolate` and never enter:

- Foundation
- Semantics
- Planning
- SQL
- execution contracts

This is the same boundary principle used for GraphQL variables and fragments.
