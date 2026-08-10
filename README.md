# Foundgine

Foundgine is being rebuilt as a small, provider-independent semantic execution
engine for .NET.

The archived V1 implementation proved the thesis. The current source is a
clean rebuild that ports only the parts that remain useful.

## Architecture

```text
API / Adapter
     |
   Resolve
     |
Semantic Request
     |
Semantic Graph
     |
 Authorize
     |
Authorized Graph
     |
  Planner
     |
Provider Plan
     |
 Provider
     |
Data Source
```

Compile-time metadata is a separate input:

```text
Domain Model
     |
AOT / Metadata Generation
     |
Metadata
     |
Static Semantic Topology
```

GraphQL, SQL, Hot Chocolate, EF, PostgreSQL, and other technologies are
adapters/providers. They are not the semantic core.

## Current build target

The first acceptance path is the Banking domain:

```text
Customer
   |
 Accounts
   |
Account
   |
Transactions
   |
Transaction
```

The first milestone is deliberately small:

1. describe the domain with metadata;
2. construct its semantic model;
3. represent a request as a semantic graph;
4. turn that graph into a provider-independent execution plan.

Only after this path is stable will a SQL provider, AOT generator, or GraphQL
adapter be added.

## Repository layout

- `src/Foundgine.Abstractions` — foundational contracts.
- `src/Foundgine.Metadata` — static domain/storage metadata.
- `src/Foundgine.Semantics` — semantic model, request graph, and resolution.
- `src/Foundgine.Planning` — provider-independent planning.
- `src/Foundgine.Execution` — provider execution boundary.
- `src/Foundgine.Aot` — future compile-time metadata generation boundary.
- `tests/` — architecture and acceptance tests.
- `archive/FoundgineV1` — historical reference implementation; not a runtime dependency.

## Porting rule

Do not migrate V1 classes mechanically.

See `PORTED-FROM-V1.md` and `PORTING-RULES.md`.
