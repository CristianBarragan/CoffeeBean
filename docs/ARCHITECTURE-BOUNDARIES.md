# Architecture Boundaries

Foundgine's architecture is intentionally layered. The public story only holds if the codebase preserves those boundaries.

## Dependency direction

```text
Foundgine.Abstractions
        ↑
Foundgine.Metadata      Foundgine.Semantics
        ↑                       ↑
        └──────────┬────────────┘
                   ↓
             Foundgine.Planning
                   ↓
             Foundgine.Execution
              ↙           ↘
     Foundgine.Sql      Foundgine.InMemory

Adapters remain outside the semantic core:

Foundgine.GraphQL.HotChocolate
Foundgine.GraphQL.HotChocolate.Mutations
Foundgine.Intent.Json
Foundgine.Aot
```

The exact project graph may contain additional edges required by implementation, but the following rules are non-negotiable.

## Rules

### 1. Abstractions stay transport/provider neutral

`Foundgine.Abstractions` must not reference SQL, GraphQL, Hot Chocolate, JSON, EF Core, Dapper, or provider implementations.

### 2. Semantics stays execution neutral

`Foundgine.Semantics` describes meaning, intent, resolution, and authorization. It must not reference SQL or GraphQL packages.

### 3. Planning stays provider neutral

`Foundgine.Planning` produces logical execution plans. Provider-specific SQL concepts must not be introduced into the planner merely because a SQL provider needs them.

### 4. Execution owns provider contracts, not provider implementations

`Foundgine.Execution` defines how a plan is handed to a provider and how results/evidence are returned. It must remain independent of SQL, GraphQL, and Hot Chocolate.

### 5. Providers lower; they do not redefine semantics

`Foundgine.Sql` and `Foundgine.InMemory` are interpretations of the same logical plan. A provider-specific limitation must not leak backward into semantics or planning as an accidental core abstraction.

### 6. Adapters translate into Foundgine

GraphQL and JSON intent are input adapters. They are not the semantic model itself and must not become required dependencies of the core.

### 7. Graphgine is historical material

The active `src/` tree must not depend on `Graphgine`. Graphgine belongs under `archive/` and must not influence the current Foundgine dependency graph.

## Boundary test policy

These rules are enforced by repository tests where practical. The purpose is not to test MSBuild itself; it is to make architectural erosion fail visibly during development.

When adding a new project, update this document first and decide which boundary it belongs to. If the dependency does not fit the model, stop and resolve the architecture before adding the reference.
