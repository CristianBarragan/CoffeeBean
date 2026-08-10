# Migration Mapping

This document is historical context, not a recommendation to preserve the old architecture.

## Historical direction

The repository previously contained:

```text
CoffeeBeanery
Graphgine
GraphQL
Hot Chocolate
Source Generators
PostgreSQL/graph providers
```

Those projects are archived.

## Current direction

The active platform is:

```text
Foundgine.Abstractions
Foundgine.Foundation
Foundgine.Metadata
Foundgine.Semantic
Foundgine.Builders
Foundgine.Planning
Foundgine.Execution.Contracts
Foundgine.Providers
```

## Conceptual migration

```text
Old GraphQL request
       ↓
GraphQL/Graphgine planning
```

becomes:

```text
Structured semantic intent
       ↓
Foundgine resolution
       ↓
Foundgine planning
```

GraphQL, REST, gRPC or MCP can later become adapters that produce the same structured intent.

## Source generation

The old generator should not be reintroduced wholesale.

The future compiler direction is narrower:

```text
Application source
       ↓
Roslyn
       ↓
semantic vocabulary
```

Runtime planning remains dynamic.
