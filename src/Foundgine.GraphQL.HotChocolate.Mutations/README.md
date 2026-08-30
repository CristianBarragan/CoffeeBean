# Foundgine.GraphQL.HotChocolate.Mutations

`Foundgine.GraphQL.HotChocolate.Mutations` is the GraphQL mutation adapter for Foundgine.

It translates GraphQL mutation input and requested result selections into Foundgine's canonical semantic mutation representation.

## Boundary

```text
GraphQL mutation
      ↓
HotChocolateMutationAdapter
      ↓
NestedMutationIntent / SemanticMutationOperationGraph
      ↓
Foundgine mutation planner
      ↓
authorization + security gate
      ↓
provider
```

The adapter is intentionally not an execution boundary.

## Main components

### `HotChocolateMutationAdapter`

Translates GraphQL mutation syntax into the semantic mutation representation.

### `GraphQLMutationSemanticConverter`

Converts nested mutation input into the canonical semantic mutation graph.

### `GraphQLMutationResultShaper`

Shapes mutation results according to the GraphQL selection requested by the caller.

## Canonical mutation graph

The adapter produces semantic operations based on:

- semantic entity identities;
- semantic field identities;
- relationship identities;
- operation labels;
- values;
- generated-value references;
- requested return fields.

It does not embed:

- SQL columns;
- SQL statements;
- ADO.NET parameters;
- provider transaction objects.

## Security boundary

Do not treat GraphQL mutation arguments as authority.

The GraphQL layer must not accept identity, tenant, audience, warrant, or provider control values as trusted execution context.

Use the separate:

`Foundgine.GraphQL.HotChocolate.MutationExecution`

package for secure mutation execution.

That executor obtains the host-owned `ISecurityExecutionContextProvider` and routes the canonical graph through `IFoundgineMutations`.

## Why translation is separate from execution

Keeping the adapter pure gives Foundgine one mutation security lifecycle across transports:

```text
GraphQL
JSON/MCP/other transport
      │
      ▼
semantic mutation graph
      │
      ▼
same mutation authorization/execution boundary
```

This prevents GraphQL from growing a second authorization implementation.

## Result shaping

Mutation results can include:

- scalar fields;
- generated identifiers;
- relationship results;
- aliases/selection shape.

The shaper works from Foundgine's provider-independent result contract.

## Errors

`GraphQLAdapterResult<T>` and mutation adaptation/result types allow translation failures to be represented without executing an invalid provider operation.

## What this package does not do

It does not:

- execute SQL;
- open database connections;
- authorize the caller;
- authenticate users;
- approve mutations;
- generate warrants;
- host GraphQL.

## Related packages

- `Foundgine.GraphQL.HotChocolate` — query-side GraphQL translation.
- `Foundgine.GraphQL.HotChocolate.MutationExecution` — secure mutation execution.
- `Foundgine.Planning` — mutation planning.
- `Foundgine.Execution` — provider execution/security boundary.
- `Foundgine.Sql` — SQL provider.

## Target framework

- .NET 9
- Hot Chocolate Language APIs
- MIT licensed
