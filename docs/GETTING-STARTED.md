# Getting started

Foundgine targets .NET 9.

The quickest way to understand it is to run the repository's SupplyChain sample and then inspect the same pipeline in the `src/` packages.

## Requirements

- .NET 9 SDK
- Git
- Docker Desktop (required for PostgreSQL-backed integration/sample execution)

## Build

From the repository root:

```bash
dotnet restore
dotnet build
```

The repository enables nullable reference types and treats warnings as errors.

## Run the unit tests

```bash
dotnet test
```

The normal unit suite is designed to run without a database.

For PostgreSQL integration tests, see [POSTGRES-E2E.md](POSTGRES-E2E.md).

## First application

At minimum an application needs:

1. a semantic model;
2. an authorization policy;
3. a provider plan compiler;
4. an execution provider.

The normal composition is:

```csharp
services.AddFoundgine(options =>
{
    options.Model = model;
    options.AuthorizationPolicy = policy;
});
```

Provider registration is separate.

For PostgreSQL, add the SQL provider and register its compiler/execution services according to the application/provider composition.

## A simple query

Typed:

```csharp
var result = await foundgine
    .Query<Customer>()
    .Select(c => new { c.Id, c.Name })
    .Where(c => c.TenantId == tenantId)
    .Take(50)
    .ExecuteAsync();
```

Dynamic:

```csharp
var result = await foundgine
    .Query("Customer")
    .Select("Id", "Name")
    .Where("TenantId", SemanticFilterOperator.Eq, tenantId)
    .Take(50)
    .ExecuteAsync();
```

Both produce the same provider-neutral semantic intent.

## Structural metadata

If the application already has metadata:

```csharp
services.AddFoundgine(options =>
{
    options
        .UseMetadata(metadata)
        .ConfigureSemantics(model =>
        {
            model.Traversal(
                "Customer",
                "transactions",
                "customerRelationships",
                "contract",
                "transactions");
        })
        .ConfigureAuthorization(auth =>
        {
            // application policy
        });
});
```

Metadata supplies structural facts. Semantic configuration supplies application meaning.

## AOT

For compile-time metadata, use `Foundgine.Aot` declarations with the `Foundgine.Aot.Generator`.

```text
attributes
   ↓
source generator
   ↓
generated metadata
   ↓
metadata/semantic model
```

See [AOT.md](AOT.md).

## JSON intent

For structured callers:

```csharp
var adapter = new JsonReadIntentAdapter();
var intent = adapter.Parse(json);

var result = await foundgine.ExecuteAsync(intent);
```

Configure `JsonReadIntentAdapterOptions` for public endpoints.

## GraphQL

Use `Foundgine.GraphQL.HotChocolate` to translate GraphQL.

For secure query execution use:

`Foundgine.GraphQL.HotChocolate.Execution`.

For mutations use:

- `Foundgine.GraphQL.HotChocolate.Mutations`;
- `Foundgine.GraphQL.HotChocolate.MutationExecution`.

The host owns authentication/security context.

## MCP

`Foundgine.MCP` exposes semantic capabilities and intent through MCP.

The host should provide an `ISecurityExecutionContextProvider` backed by authenticated request/session state.

Do not allow MCP arguments to choose tenant, identity, warrant, or provider credentials.

## AI

`Foundgine.AI` integrates with `Microsoft.Extensions.AI`.

The model can call semantic tools:

```text
LLM
 ↓
Foundgine.AI
 ↓
Foundgine
 ↓
authorization + planning
 ↓
provider
```

The model is an untrusted producer of intent.

## What to read next

- [Why Foundgine](WHY-FOUNDGINE.md)
- [Architecture](ARCHITECTURE.md)
- [Open Intent API](OPEN-INTENT-API.md)
- [Authorization](AUTHORIZATION.md)
- [Security](SECURITY.md)
- [PostgreSQL E2E](POSTGRES-E2E.md)

For package-specific details, see the `README.md` in each `src/Foundgine.*` project.
