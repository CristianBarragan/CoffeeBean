# Foundgine — AI context

Foundgine is a **semantic execution layer for .NET**.

Canonical description: Foundgine converts structured application intent into deterministic, authorization-preserving execution plans that can be executed by a physical provider.

Its purpose is to separate **what an application means** from **how a provider executes it**.

```text
Input
  ↓
Semantic Request
  ↓
Resolution
  ↓
Authorization
  ↓
Semantic Graph
  ↓
Execution Plan
  ↓
Provider
```

## Product identity

Foundgine owns semantics, intent resolution, authorization, provider-independent planning, execution coordination, and execution evidence. It does not own the transport, database engine, ORM persistence model, LLM/agent runtime, workflow engine, or identity system.

AI is an important consumer of the semantic boundary, not the definition of the product.

Capability discovery is a first-class semantic interface: `DescribeCapabilities()` exposes a deterministic, policy-scoped capability graph. AI adapters can use it to construct structured intent, but the capability document is never treated as an authorization decision.

## Core projects

`Foundgine.Abstractions` contains stable IDs and small contracts.

`Foundgine.Metadata` describes entities, fields, relationships, and storage mappings.

`Foundgine.Semantics` owns semantic meaning, request resolution, granular authorization, and capability discovery.

`Foundgine.Planning` creates provider-independent logical plans.

`Foundgine.Execution` defines provider execution and result materialization.

`Foundgine.Sql` is the SQL execution provider. `Foundgine.InMemory` is a deliberately small CLR-backed proof provider used to demonstrate provider independence for the tested plan subset.

`Foundgine.Aot` and `Foundgine.Aot.Generator` provide compile-time metadata.

`Foundgine.Intent.Json` and `Foundgine.GraphQL.HotChocolate*` are input adapters.

## Current proof

The repository proves:

- semantic entities, fields, relationships, and identities;
- resolution and granular authorization;
- entity/field/relationship read/write capabilities;
- conditional authorization predicates preserved into execution plans;
- mutation write authorization;
- query and mutation planning;
- nested/dependency-aware mutations;
- SQL compilation and SQLite execution;
- AOT metadata generation;
- JSON structured intent;
- GraphQL queries and mutations;
- GraphQL variables, fragments, aliases, directives, operation selection, input coercion, schema generation, and structured adapter errors;
- relationship filters/order, aggregate filters/order, and cursor pagination.

The tests are the authoritative evidence for these claims.

## Architectural rules

1. Keep the semantic core protocol-neutral.
2. Keep planning provider-neutral.
3. Keep physical storage details in providers.
4. Keep GraphQL/JSON concerns in adapters.
5. Treat external intent as untrusted.
6. Capability discovery is advisory context, never an authorization cache.
7. Preserve authorization predicates into provider execution semantics.
8. Prefer small contracts over compatibility layers.
9. Do not port archive code unless the current architecture needs the capability.

## Non-goals

Foundgine is not an ORM replacement, GraphQL server, database, workflow engine, or generic agent framework.

`archive/` contains historical implementations and should not be used as an active dependency.
