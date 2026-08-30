# Foundgine.GraphQL.HotChocolate.Execution

`Foundgine.GraphQL.HotChocolate.Execution` provides the secure query execution boundary for the Hot Chocolate adapter.

The package exists so GraphQL translation and Foundgine execution remain separate responsibilities.

## Boundary

```text
GraphQL
  ↓
Foundgine.GraphQL.HotChocolate
  ↓
SemanticRequest
  ↓
FoundgineHotChocolateQueryExecutor
  ↓
host-owned security context
  ↓
Foundgine execution
  ↓
provider
```

## `FoundgineHotChocolateQueryExecutor`

The executor combines:

- the Foundgine runtime;
- the Hot Chocolate semantic adapter;
- an `ISecurityExecutionContextProvider`.

Conceptually:

```csharp
var executor = new FoundgineHotChocolateQueryExecutor(
    foundgine,
    new HotChocolateSemanticAdapter(model),
    securityContextProvider);

var result = await executor.ExecuteAsync(
    graphqlQuery,
    variables,
    operationName);
```

The host supplies the caller's security context. `ExecuteAsync` returns a `GraphQLQueryExecutionResult` (the raw `ExecutionResult` plus a `GraphQLResultShape` describing aliases/nesting for response mapping) and throws on translation, security, or execution failure.

For hosts that want failures surfaced as ordinary GraphQL response errors instead of thrown exceptions, use `TryExecuteAsync(...)`, which maps translation/security/execution failures to a stable `GraphQLAdapterError` via `GraphQLAdapterResult<T>` instead of throwing.

## Why the security context is separate

GraphQL input is untrusted.

A GraphQL payload must not be able to claim:

```text
tenant = tenant-b
role = administrator
warrant = ...
```

The executor therefore obtains authority from the host-owned context provider.

```text
authenticated request
       ↓
ISecurityExecutionContextProvider
       ↓
executor
       ↓
semantic execution
```

## Execution responsibilities

The executor:

1. parses/adapts GraphQL into semantic intent;
2. obtains trusted security context;
3. attaches the context to execution;
4. delegates to the Foundgine runtime;
5. returns a GraphQL-oriented result.

It does not implement SQL or provider execution itself.

## Use this package when

Use it when your application wants a secure, standard query path:

```text
ASP.NET + Hot Chocolate
        ↓
Foundgine.GraphQL.HotChocolate.Execution
        ↓
Foundgine
        ↓
provider
```

## Do not use the adapter alone as a security boundary

`Foundgine.GraphQL.HotChocolate` remains useful as a pure translation component.

If an application manually wires translation to execution, it is responsible for correctly attaching the trusted security context.

The dedicated executor exists to make the intended secure path explicit.

## Mutations

This package is query-side only.

For mutations use the separate:

`Foundgine.GraphQL.HotChocolate.MutationExecution`

package.

## Related packages

- `Foundgine.GraphQL.HotChocolate`
- `Foundgine`
- `Foundgine.Semantics`
- `Foundgine.Execution`

## Target framework

- .NET 9
- MIT licensed
