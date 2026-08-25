# Foundgine.GraphQL.HotChocolate.Execution

Secure query execution integration for the Foundgine Hot Chocolate adapter.

`Foundgine.GraphQL.HotChocolate` remains a pure GraphQL-to-semantic adapter. This package adds the optional execution boundary that combines the adapter with `IFoundgine` and requires a host-supplied `ISecurityExecutionContextProvider`.

```csharp
var executor = new FoundgineHotChocolateQueryExecutor(
    foundgine,
    new HotChocolateSemanticAdapter(model),
    securityContextProvider);

var result = await executor.ExecuteAsync(graphqlQuery, variables, operationName);
```

GraphQL request payloads cannot provide identity, tenant, audience, or warrant material. The host establishes that context before execution.
