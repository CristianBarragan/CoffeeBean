# Foundgine.GraphQL.HotChocolate.MutationExecution

`Foundgine.GraphQL.HotChocolate.MutationExecution` is the secure execution boundary for Foundgine GraphQL mutations.

It exists to ensure GraphQL writes use the same canonical mutation security and execution path as other Foundgine mutation transports.

## Boundary

```text
GraphQL payload
      ↓
Hot Chocolate mutation adapter
      ↓
SemanticMutationOperationGraph
      ↓
host-owned security context
      ↓
warrant / tenant / audience / resource checks
      ↓
semantic mutation authorization
      ↓
security-invariant certification
      ↓
IFoundgineMutations
      ↓
provider
```

## `FoundgineHotChocolateMutationExecutor`

The executor obtains caller authority only from `ISecurityExecutionContextProvider`.

```csharp
var executor = new FoundgineHotChocolateMutationExecutor(
    mutations,
    new HotChocolateMutationAdapter(model),
    mutationSchema,
    securityContextProvider);

var result = await executor.ExecuteAsync(
    graphqlMutation,
    variables,
    operationName);
```

It combines the Foundgine mutation runtime (`IFoundgineMutations`), the Hot Chocolate mutation adapter, an `IMutationSchema` (the structural contract the semantic mutation graph is converted against), and the host-owned security context provider.

It then:

1. translates the GraphQL mutation;
2. obtains trusted security context;
3. constructs the canonical semantic mutation graph;
4. invokes `IFoundgineMutations`;
5. returns a GraphQL-oriented execution result.

## Why this package exists

GraphQL translation itself is not authorization.

A translation adapter can correctly parse:

```graphql
mutation {
  updateInventory(...)
}
```

and still be unsafe if a host lets request arguments define the caller's authority.

This package makes the secure composition explicit.

## Same security boundary as non-GraphQL mutations

The desired architecture is:

```text
GraphQL ─┐
MCP ─────┤
JSON ────┤
Code ────┘
     ↓
SemanticMutationOperationGraph
     ↓
IFoundgineMutations
     ↓
same authorization/security/execution boundary
```

## High-assurance controls

The downstream mutation runtime can enforce:

- warrant validation;
- tenant/audience/resource scope;
- semantic authorization;
- security invariant certification;
- replay protection;
- plan/approval requirements;
- provider execution conformance.

This package does not duplicate those checks.

## What this package does not do

It does not:

- authenticate users;
- create authority;
- generate SQL;
- execute a database command directly;
- bypass `IFoundgineMutations`.

## Related packages

- `Foundgine.GraphQL.HotChocolate.Mutations`
- `Foundgine`
- `Foundgine.Execution`
- `Foundgine.Semantics`
- `Foundgine.Sql`

## Target framework

- .NET 9
- MIT licensed
