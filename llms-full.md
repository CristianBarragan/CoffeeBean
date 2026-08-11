# Foundgine — AI context

Foundgine is a small, provider-independent semantic execution engine for .NET.

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

## Core projects

`Foundgine.Abstractions` contains stable IDs and small contracts.

`Foundgine.Metadata` describes entities, fields, relationships, and storage mappings.

`Foundgine.Semantics` owns semantic meaning, request resolution, and authorization.

`Foundgine.Planning` creates provider-independent logical plans.

`Foundgine.Execution` defines provider execution and result materialization.

`Foundgine.Sql` is the current SQL provider.

`Foundgine.Aot` and `Foundgine.Aot.Generator` provide compile-time metadata.

`Foundgine.Intent.Json` and `Foundgine.GraphQL.HotChocolate*` are input adapters.

## Current proof

The repository proves:

- semantic entities, fields, relationships, and identities;
- resolution and authorization;
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
6. Prefer small contracts over compatibility layers.
7. Do not port archive code unless the current architecture needs the capability.

## Non-goals

Foundgine is not an ORM replacement, GraphQL server, database, workflow engine, or generic agent framework.

`archive/` contains historical implementations and should not be used as an active dependency.
