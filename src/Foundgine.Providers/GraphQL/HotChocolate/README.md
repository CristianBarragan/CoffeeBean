# Foundgine.Providers — GraphQL/HotChocolate

The secure execution engine for the Foundgine Hot Chocolate GraphQL adapter.

## What is in this folder

- `FoundgineHotChocolateQueryExecutor` — executes GraphQL queries through the
  Foundgine semantic execution boundary.
- `FoundgineHotChocolateMutationExecutor` — executes GraphQL mutations through
  the Foundgine mutation authorization/execution boundary.
- Host-owned security execution-context integration for both.

## Why it lives here, not in Extensions

`Foundgine.Extensions.GraphQL.HotChocolate` (schema adapters, query/mutation
translation, result shaping) performs GraphQL *translation*. This folder
performs secure *execution* — the same role the other folders under
`Foundgine.Providers` play for MCP tools, storage, and model providers.

```text
GraphQL
  ↓
Hot Chocolate adapter (Foundgine.Extensions.GraphQL.HotChocolate)
  ↓
FoundgineHotChocolateQueryExecutor / FoundgineHotChocolateMutationExecutor (here)
  ↓
Foundgine authorization / planning / execution
  ↓
provider
```

A GraphQL transport must not become the authority for identity, tenant, or
authorization state. `Foundgine.Providers` depends on `Foundgine.Extensions`
for the adapter/translation types these executors call into.

Use this folder together with `Foundgine.Extensions.GraphQL.HotChocolate`
when GraphQL requests should execute through the canonical Foundgine
security boundary.
