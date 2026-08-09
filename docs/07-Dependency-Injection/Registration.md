# Registration

The current canonical Banking proof does not require a framework-wide registration package.

The first priority is proving the contracts.

## Target registration shape

A future application may compose:

```text
Semantic model
Resolver
Planner
Policy
Execution provider
Verifier
Evidence sink
```

through the application's DI container.

Example shape:

```csharp
services
    .AddFoundgine(...)
    .AddExecutionProvider(...)
    .AddPolicy(...)
    .AddEvidence(...);
```

This is illustrative only; the final API should not be designed until the underlying contracts have been proven.

## Integration principle

Transport adapters such as MCP or GraphQL should compose Foundgine services from outside the core.

They should not force their own service abstractions into `Foundgine.Foundation`.
