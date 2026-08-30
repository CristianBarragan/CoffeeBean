# Foundgine.Semantics

`Foundgine.Semantics` is the provider-independent meaning layer of Foundgine.

It defines the application's semantic model, open intent, resolution, semantic validation, authorization, relationship traversal, mutation meaning, security context contracts, capability discovery, and semantic execution inputs.

It does not know SQL, GraphQL, PostgreSQL, MCP, or any other transport/provider implementation.

## The semantic boundary

```text
External intent
      ↓
Resolve
      ↓
Validate
      ↓
Normalize
      ↓
Canonical semantic operation
      ↓
Authorize
      ↓
Planning
```

The semantic layer is where a request becomes meaningful in the application's vocabulary.

## What the semantic model represents

A `SemanticModel` describes entities and their relationships using semantic identities.

A semantic entity can contain:

- `EntityId`;
- public semantic name;
- primary identity;
- semantic fields;
- relationships;
- aliases;
- optional CLR model type.

A semantic field can carry provider-independent type and capability information.

Relationships carry:

- `RelationshipId`;
- semantic name;
- target entity;
- cardinality;
- aliases.

## Building a model

Manual authoring is supported:

```csharp
var model = new SemanticModelBuilder()
    .Entity<Customer>(customerId, "Customer", entity => entity
        .Identity(x => x.Id)
        .Field(x => x.Id)
        .Field(x => x.Name)
        .Field(x => x.TenantId))
    .Entity<Order>(orderId, "Order", entity => entity
        .Identity(x => x.Id)
        .Field(x => x.Id)
        .Field(x => x.CustomerId))
    .Relationship<Customer, Order>(
        customerId,
        "orders",
        customer => customer.Id,
        orderId,
        order => order.CustomerId,
        RelationshipCardinality.Many)
    .Build();
```

Typed field selectors are rooted in the declared CLR model type. They are not selectors over provider metadata.

## Strict typed mode

`RequireTypedEntities()` opts into a stronger authoring rule.

When enabled:

- the untyped entity registration path is rejected;
- typed entities must use a CLR model explicitly marked with `SemanticEntityAttribute`.

This is useful when an application wants to prevent semantic fields from drifting away from deliberately exposed domain/application model types.

## Structural discovery

When `Foundgine.Metadata` is available:

```csharp
var model = metadata
    .FromMetadata()
    .Traversal(
        "Customer",
        "transactions",
        "customerRelationships",
        "contract",
        "transactions")
    .Build();
```

Structural metadata supplies ordinary entities, fields, primary identities, and direct relationships. Semantic configuration supplies meaning that cannot safely be inferred from storage facts.

## Logical traversals

Foundgine supports semantic traversals that hide intermediate relationships from the caller.

For example:

```text
Customer
  → CustomerRelationship
      → Contract
          → Transaction
```

can be exposed as:

```text
Customer.transactions
```

Resolution expands the traversal to the real path before authorization and planning.

This is a security property as well as a convenience:

```text
Customer.transactions
        ↓
Customer
  ↓
CustomerRelationship
  ↓
Contract
  ↓
Transaction
        ↓
authorize every required node/edge
```

A logical shortcut can never tunnel through a denied intermediate entity.

## Open read intent

The semantic read model supports:

- entity selection;
- field selection;
- relationship selection;
- field filters;
- logical AND/OR filters;
- relationship quantifiers;
- ordering;
- relationship-path ordering;
- limit/offset;
- cursor pagination;
- aggregate-aware ordering;
- semantic aliases.

Typed and dynamic query builders both produce the same `ReadIntent`.

## Semantic validation

Resolution is a correctness boundary, not merely a name lookup.

The semantic layer validates:

- referenced entities;
- referenced fields;
- relationship targets;
- field capabilities;
- scalar/list value compatibility;
- query controls;
- pagination combinations;
- relationship topology;
- aggregate semantics.

`SemanticGraphValidator` verifies graph consistency before planning.

Invalid input should fail before a provider receives it.

## Types and values

`SemanticType` expresses provider-independent meaning.

`SemanticValue` provides a canonical semantic value representation while compatibility constructors can still accept ordinary CLR values.

The semantic layer can therefore validate a request without knowing whether the provider eventually represents a value as a PostgreSQL parameter, an in-memory object, or another physical representation.

## Query controls

`SemanticQueryOptionsValidator` protects the execution boundary from malformed pagination and ordering combinations.

Cursor pagination also uses the root identity as a deterministic tie-breaker when necessary, making page traversal stable when the requested ordering is not unique.

## Authorization

Authorization is part of semantic execution, but identity/authentication remains host-owned.

The policy can reason about:

- entity read/write;
- field read/write;
- relationship read/write;
- conditional predicates;
- named operations/capabilities.

Conditional authorization is represented provider-independently. A predicate can survive into planning and be lowered by the provider.

The important distinction is:

```text
Capability discovery = advisory description
Authorization = authoritative decision
```

A capability snapshot must never be treated as a reusable authorization token.

## Security execution context

`SecurityExecutionContext` carries host-established execution authority into the semantic request.

`ISecurityExecutionContextProvider` lets transport adapters obtain that context from the application host.

The semantic layer does not invent identity from GraphQL, MCP, JSON, or AI payloads.

## Security warrants

The security namespaces provide contracts and helpers for warrant-backed execution, including:

- trusted key resolution;
- issuer validation;
- replay protection;
- revocation/delegation support;
- execution-time security context.

These facilities remain provider-independent. A database-specific implementation belongs in the provider layer.

## Mutation semantics

Mutation semantics are represented separately from read execution.

The mutation model includes:

```text
SemanticMutationOperationGraph
  ├── entity identity
  ├── field identity
  ├── relationship identity
  ├── values
  ├── generated-value references
  └── requested result fields
```

The semantic mutation graph does not contain SQL columns or provider-specific transaction operations.

`SemanticMutationIntentBuilder` provides an open authoring surface:

```csharp
var graph = new SemanticMutationIntentBuilder(model)
    .Create("PurchaseOrder", "order")
        .Set("SupplierId", supplierId)
        .Return("Id")
    .Create("PurchaseOrderLine", "line")
        .SetFrom("PurchaseOrderId", "order", "Id")
        .Set("Quantity", 25m)
        .Return("Id", "PurchaseOrderId")
    .Build();
```

The builder creates meaning. Planning and execution decide how that meaning is performed.

## Candidate resolution and retrieval

The resolver can use candidate-source contracts for ambiguous names and references.

The semantic abstraction distinguishes:

- exact/normal candidate sources;
- approximate retrieval;
- advanced retrieval;
- provenance/evidence.

Provider-specific retrieval can therefore contribute evidence without becoming the authority over semantic resolution.

`Foundgine.Sql` supplies a PostgreSQL retrieval implementation for this boundary.

## Semantic contracts and snapshots

`SemanticContractSnapshot` represents an immutable runtime contract.

The application should build/configure the semantic model during startup and execute requests against the frozen contract.

This supports:

- deterministic versioning;
- concurrent reuse;
- plan/cache identity;
- safer request processing.

## What does not belong here

Do not add:

- SQL generation;
- SQL parameters;
- GraphQL AST nodes;
- Hot Chocolate execution objects;
- PostgreSQL-specific operators;
- database connection management;
- LLM calls.

Those are adapter/provider responsibilities.

## Related packages

| Package | Role |
|---|---|
| `Foundgine.Abstractions` | Shared identities/contracts |
| `Foundgine.Metadata` | Structural discovery |
| `Foundgine.Planning` | Provider-independent planning and rewrites |
| `Foundgine.Execution` | Provider execution boundary |
| `Foundgine.Intent.Json` | JSON transport adapter |
| `Foundgine.GraphQL.HotChocolate` | GraphQL translation |
| `Foundgine.MCP` | MCP transport |
| `Foundgine.AI` | Microsoft.Extensions.AI integration |

## Architectural rule

The semantic layer answers:

> **What does this request mean, and is this actor allowed to express that meaning?**

It must not answer:

> **How do I write the SQL?**

That distinction is the foundation of Foundgine's provider independence.
