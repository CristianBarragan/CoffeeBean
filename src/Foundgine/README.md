# Foundgine

`Foundgine` is the application-facing runtime facade for Foundgine's semantic execution architecture.

It gives an application one entry point for turning open, structured intent into authorized execution while keeping semantic modelling, planning, and providers behind explicit boundaries.

## The core idea

Foundgine separates:

```text
What the caller wants
        ↓
What the application exposes
        ↓
What this caller may use
        ↓
How the operation should execute
        ↓
Which provider executes it
```

The public runtime pipeline is:

```text
ReadIntent / MutationIntent
        ↓
Semantic resolution + validation
        ↓
Authorization
        ↓
Provider-independent plan
        ↓
Execution boundary
        ↓
Provider
        ↓
ExecutionResult
```

The transport does not need to know SQL, and the provider does not need to know GraphQL.

## What this package owns

The core package contains:

- `IFoundgine` — query execution contract;
- `IFoundgineMutations` — mutation execution contract;
- `FoundgineEngine` — read execution coordination;
- `FoundgineMutationEngine` — mutation security/execution coordination;
- `FoundgineOptions` — application composition;
- typed and dynamic query builders;
- the open mutation builder entry point;
- plan approval and mutation result contracts;
- dependency-injection registration helpers.

The lower-level packages remain independently usable when an application needs more control.

## Installation

```bash
dotnet add package Foundgine
```

A provider must also be registered. For example, PostgreSQL applications normally use `Foundgine.Sql`.

## Minimal composition

The application supplies a semantic model, authorization policy, planner/compiler, and execution provider.

The convenient registration path is:

```csharp
services.AddFoundgine(options =>
{
    options.Model = semanticModel;
    options.AuthorizationPolicy = authorizationPolicy;
});
```

When structural metadata is available, the model can instead be discovered and enriched:

```csharp
services.AddFoundgine(options =>
{
    options
        .UseMetadata(metadata)
        .ConfigureSemantics(model =>
        {
            // Add application-specific semantic meaning here.
        })
        .ConfigureAuthorization(auth =>
        {
            // Add application policy here.
        });
});
```

`AddFoundgine` freezes the semantic model into a runtime contract snapshot. The mutable builder is therefore not the object used by concurrent request execution.

## Query authoring

Foundgine supports two query surfaces that converge on the same provider-neutral `ReadIntent`.

### Typed

```csharp
var result = await foundgine
    .Query<Customer>()
    .Select(c => new { c.Id, c.Name })
    .Include(c => c.Orders, orders =>
        orders.Select(o => new { o.Id, o.OrderDate }))
    .Where(c => c.TenantId == tenantId)
    .OrderBy(c => c.Name)
    .Take(50)
    .ExecuteAsync();
```

The typed builder provides compile-time property selection and currently supports simple property comparisons (`==`, `!=`) plus boolean `&&` / `||` composition in `Where`.

### Dynamic

Dynamic intent is useful when the caller does not have a CLR type at compile time:

```csharp
var result = await foundgine
    .Query("Customer")
    .Select("Id", "Name")
    .Where("TenantId", SemanticFilterOperator.Eq, tenantId)
    .OrderBy("Name")
    .Take(50)
    .ExecuteAsync();
```

Dynamic names are resolved against the semantic model. They do not become SQL identifiers directly.

## Query controls

Both query surfaces support:

- selection;
- relationship inclusion;
- filtering;
- ordering;
- limit;
- offset;
- cursor (`After`);
- security execution context.

Dynamic queries additionally support:

- related filters;
- `AndWhere`;
- `OrWhere`;
- ordering through a relationship path.

Invalid limits and offsets are rejected at the authoring/semantic validation boundary.

## Open intent

Foundgine does not require an application to predefine a method for every future query.

```text
Application API
GraphQL
JSON
MCP
AI
   │
   ▼
ReadIntent
   │
   ▼
same semantic pipeline
```

This is particularly useful for agent and integration scenarios, where the set of useful questions is discovered at runtime.

Open intent does **not** mean open authority. The semantic model and authorization policy remain authoritative.

## Security context

Security context is host-owned.

A caller should not be able to choose its own:

- tenant;
- identity;
- audience;
- warrant;
- authorization authority.

Adapters such as MCP and GraphQL therefore obtain security context from the host rather than accepting it as ordinary user-controlled request data.

For direct application usage, `WithSecurity(...)` can attach a `SecurityExecutionContext` to a query.

## Warrants, execution-time revalidation, and resource limits

`FoundgineOptions` exposes several security-related composition points beyond the semantic model and authorization policy:

- `WarrantKeyResolver` (`ISecurityWarrantKeyResolver`) and `ExpectedWarrantIssuer` — trusted key resolution and issuer validation for signed semantic security warrants.
- `WarrantReplayStore` (`ISecurityWarrantReplayStore`) — replay protection for warrant-backed requests.
- `SecurityResourceLimits` — canonical engine-side bounds on untrusted semantic request complexity (selection depth, filter depth/nodes, and similar structural limits), defaulted but overridable.
- `ExecutionAuthorizationRevalidator` (`IExecutionAuthorizationRevalidator`) and `ExecutionAuthorizationAuthorityResolver` — an optional trusted authority consulted immediately before provider execution, so a plan compiled earlier can be re-checked against current authority state (see `Foundgine.Security.Authority` for a full recovery/control-plane implementation of that authority).

None of these are required for a basic application; they exist for hosts that need warrant-backed, revalidated, or resource-bounded execution.

## Mutations composition

Mutation execution has a separate interface because writes have stronger correctness and security requirements.

The core package exposes:

```csharp
var graph = mutations.Mutate(model)
    .Create("PurchaseOrder", "order")
        .Set("SupplierId", supplierId)
        .Return("Id")
    .Build();
```

The resulting semantic mutation graph is consumed by the existing mutation planning and security boundary.

For high-assurance writes, use the `IFoundgineMutations` execution path rather than treating the builder as an authorization mechanism.

## Plan cache

`FoundgineOptions.PlanCache` can provide a compiled-provider-plan cache.

The security rule is important:

```text
resolve request
      ↓
authorize request
      ↓
cache/compile provider plan
      ↓
execute with current context
```

A cache must never turn an authorization predicate into an authorization-free plan.

## Dependency injection boundary

`AddFoundgine(...)` registers the runtime facade and its immutable semantic contract. Provider services are expected to register:

- `IProviderPlanCompiler`;
- `IExecutionProvider`;
- and, for mutations, the appropriate mutation schema/provider.

This keeps provider-specific construction outside the core package.

## What this package does not do

`Foundgine` does not:

- expose GraphQL;
- host MCP;
- call an LLM;
- generate SQL itself;
- act as an ORM;
- manage authentication;
- manage an authorization server;
- own database connections;
- provide a distributed workflow engine.

Those concerns belong to surrounding packages or the application.

## Recommended package choices

For a typical PostgreSQL application:

```text
Foundgine
├── Foundgine.Semantics
├── Foundgine.Metadata
├── Foundgine.Planning
├── Foundgine.Execution
└── Foundgine.Sql
```

Add only the adapters you actually expose:

```text
+ Foundgine.GraphQL.HotChocolate
+ Foundgine.GraphQL.HotChocolate.Execution
+ Foundgine.MCP
+ Foundgine.Intent.Json
+ Foundgine.AI
+ Foundgine.Aot / Foundgine.Aot.Generator
```

## Related documentation

Start with:

- `docs/GETTING-STARTED.md`
- `docs/ARCHITECTURE.md`
- `docs/OPEN-INTENT-API.md`
- `docs/AUTHORIZATION.md`
- `docs/SECURITY.md`

## Target framework

- .NET 9
- MIT licensed
