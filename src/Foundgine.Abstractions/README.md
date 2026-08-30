# Foundgine.Abstractions

`Foundgine.Abstractions` is the small, provider-independent contract layer shared by the Foundgine runtime.

It defines the stable vocabulary that lets semantic modelling, authorization, planning, execution, AOT generation, and providers communicate without depending on one another's implementation details.

## Responsibility

This package answers:

> **What stable concepts must cross Foundgine layer boundaries?**

It intentionally does **not** answer:

- how a request is resolved;
- how a plan is optimized;
- how SQL is generated;
- how a database is accessed;
- how GraphQL or MCP requests are parsed.

Those responsibilities live in higher layers.

## Main contracts and value types

The package contains the foundational identities and contracts used throughout the repository.

### Stable identifiers

Foundgine uses explicit identities for different semantic/storage concepts:

```text
ModelId
  ├── EntityId
  ├── FieldId
  ├── RelationshipId
  ├── ConnectionId
  ├── ColumnId
  └── AuthorizationId
```

These are deliberately different types. A relationship identifier must not accidentally be used where a database column identifier is expected.

Identifiers support deterministic construction where the architecture requires stable identity. For example, semantic relationships can derive an identity from stable semantic names rather than source-file ordering.

### Authorization vocabulary

Authorization contracts include:

- `AuthorizationDecision`;
- `AuthorizationAccess`;
- `AuthorizationOperation`;
- `AuthorizationOperationName`;
- `AuthorizationPredicate`.

The predicate representation is provider-independent. A provider may lower a predicate to SQL or another physical representation, but the abstraction layer does not know that representation.

### Mutation contracts

`IMutationSchema` and the related mutation schema records describe the structural information required by the mutation planner/provider boundary.

The schema exposes facts such as:

- entity fields;
- physical column correspondence;
- primary-key information;
- relationship key mappings.

It does not execute mutations.

## Design rule

Keep this package boring.

If a type needs SQL, GraphQL, Hot Chocolate, PostgreSQL, MCP, Microsoft.Extensions.AI, reflection-heavy runtime discovery, or provider-specific execution behavior, it probably does not belong here.

The dependency direction is intentionally one-way:

```text
Foundgine.Abstractions
        ▲
        │
  shared contracts
        │
 ┌──────┼────────┬──────────┐
 │      │        │          │
Semantics Metadata Planning Execution
 │      │        │          │
 └──────┴────────┴──────────┘
```

The arrows represent dependency on the stable vocabulary, not execution flow.

## JSON representation

The identifier types include JSON converters where they cross JSON-facing boundaries. Serialization is kept at the identifier boundary so adapters do not need to invent their own wire representation.

## When to reference this package

Reference `Foundgine.Abstractions` when an application or extension package needs to:

- declare semantic/storage identities;
- implement a Foundgine provider contract;
- implement authorization predicates;
- describe mutation schema;
- share Foundgine contracts without taking a dependency on the complete runtime.

Most application users will normally reference `Foundgine` rather than assembling the architecture manually.

## Compatibility principle

The abstractions package is deliberately smaller than the rest of Foundgine. Changes here have a wider blast radius because many packages consume these contracts.

When extending it:

1. prefer a new strongly typed value over reusing an unrelated identifier;
2. keep provider details out;
3. preserve deterministic identity semantics;
4. add serialization behavior only when a real boundary requires it;
5. test contract behavior independently of a database.

## Related packages

| Package | Responsibility |
|---|---|
| `Foundgine.Semantics` | Application meaning, intent, resolution, and authorization |
| `Foundgine.Metadata` | Structural metadata and discovery |
| `Foundgine.Planning` | Provider-independent execution planning |
| `Foundgine.Execution` | Provider execution boundary |
| `Foundgine.Aot` | AOT declarations |
| `Foundgine.Aot.Generator` | Compile-time metadata generation |

## Target framework

- .NET 9
- MIT licensed

`Foundgine.Abstractions` is a library package, not an application framework by itself.
