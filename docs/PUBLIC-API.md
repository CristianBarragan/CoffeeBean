# Public API

Foundgine's public API is organized around a small common path with explicit advanced boundaries.

## Common application surface

The normal application entry point is `IFoundgine`.

For reads, the main authoring surface is:

```csharp
foundgine.Query<Customer>()
```

or:

```csharp
foundgine.Query("Customer")
```

The typed and dynamic forms converge on `ReadIntent`.

## Typed query API

`TypedQuery<T>` supports:

```text
Select
Include
Where
OrderBy
Take
Skip
After
WithSecurity
ToIntent
ExecuteAsync
```

The current typed filter compiler supports direct property comparisons:

```csharp
x => x.Id == id
x => x.Name != name
x => x.TenantId == tenantId && x.IsActive == true
```

More complex semantic predicate algebra belongs in the semantic/planning layers rather than being silently interpreted by the typed convenience API.

## Dynamic query API

`DynamicQuery` supports:

```text
Select
Include
Where
WhereRelated
AndWhere
OrWhere
OrderBy
OrderByPath
Take
Skip
After
WithSecurity
ToIntent
ExecuteAsync
```

Dynamic names are still resolved against the semantic model.

## Semantic intent

The canonical read request is `ReadIntent`.

It represents caller intent without binding it to:

- SQL;
- GraphQL;
- MCP;
- a specific provider.

## Mutation API

`IFoundgineMutations` is the runtime boundary for mutations.

`SemanticMutationIntentBuilder` is the open authoring surface:

```csharp
var graph = new SemanticMutationIntentBuilder(model)
    .Create("Order", "order")
        .Set("CustomerId", customerId)
        .Return("Id")
    .Build();
```

The builder is not an authorization mechanism.

## Application configuration

`FoundgineOptions` supports:

- `Model`;
- `Metadata`;
- semantic configuration;
- authorization configuration/policy;
- provider plan cache;
- security warrant services;
- security resource limits;
- execution-time authorization revalidation;
- mutation schema/provider.

Use `AddFoundgine(...)` for normal DI composition.

## Lower-level APIs

Advanced integrations can consume:

- `Foundgine.Semantics`;
- `Foundgine.Planning`;
- `Foundgine.Execution`;
- `Foundgine.Metadata`;
- provider packages.

These expose more of the architecture intentionally.

The lower-level APIs are useful for:

- custom providers;
- custom transport adapters;
- advanced planning;
- tests;
- tooling.

## API layering rule

Prefer the highest-level API that solves the application problem.

```text
ordinary application
       ↓
Foundgine

custom intent adapter
       ↓
Foundgine.Semantics

custom planner/provider
       ↓
Planning + Execution

custom physical provider
       ↓
Execution + provider package
```

Do not make application code depend on provider internals merely to construct a query.

## Security-sensitive APIs

The following concepts should remain explicit rather than hidden:

- `SecurityExecutionContext`;
- `ISecurityExecutionContextProvider`;
- authorization policy;
- warrant validation;
- replay protection;
- provider security conformance;
- mutation approval/execution boundary.

The public API must not make it easier to accidentally replace trusted context with request data.

## Versioning

The repository is currently on the 1.1.7 release line.

The most stable conceptual contracts are:

```text
semantic identity
semantic model
ReadIntent
provider-independent plan
execution/provider boundary
```

When changing these, update the affected adapter/provider tests rather than adding compatibility shims that blur the architecture.

## Where the implementation lives

| Area | Package |
|---|---|
| Facade | `Foundgine` |
| Contracts/IDs | `Foundgine.Abstractions` |
| Semantics | `Foundgine.Semantics` |
| Metadata | `Foundgine.Metadata` |
| Planning | `Foundgine.Planning` |
| Execution | `Foundgine.Execution` |
| SQL | `Foundgine.Sql` |
| InMemory | `Foundgine.InMemory` |
| AOT | `Foundgine.Aot`, `Foundgine.Aot.Generator` |
| JSON | `Foundgine.Intent.Json` |
| GraphQL | `Foundgine.GraphQL.HotChocolate*` |
| MCP | `Foundgine.MCP` |
| AI | `Foundgine.AI` |
| Authority recovery | `Foundgine.Security.Authority` |
