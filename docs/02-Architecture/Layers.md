[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Layers**

# Layers

The active repository should be understood as a small execution platform, not as a collection of transport-specific products.

## Active project responsibilities

| Project | Responsibility |
|---|---|
| `Foundgine.Abstractions` | stable platform contracts |
| `Foundgine.Foundation` | generic primitives and CQRS foundations |
| `Foundgine.Metadata` | entity, column, relationship and join metadata |
| `Foundgine.Diagnostics` | diagnostics infrastructure |
| `Foundgine.Builders` | logical query-plan structures |
| `Foundgine.Planning` | dynamic query and mutation planning |
| `Foundgine.Execution.Contracts` | execution context, rows, provider plans and provider contracts |
| `Foundgine.Providers` | provider compilation/execution |
| `Foundgine.Samples.Banking` | canonical end-to-end proof |

## Target semantic layers

The product layer will add concepts without forcing them all into separate projects immediately:

```text
Semantic Domain
    ↓
Resolution
    ↓
Policy
    ↓
Planning
    ↓
Execution
    ↓
Verification
    ↓
Evidence
```

The implementation should follow responsibility boundaries, not create projects prematurely.

## External adapters

These should remain outside the core:

```text
MCP
GraphQL
REST
gRPC
Semantic Kernel
LLM providers
EF Core
Dapper
Temporal
Kafka
OpenTelemetry
```

They translate into or consume Foundgine contracts.

## Compile-time compiler

A future Roslyn compiler can generate semantic descriptors.

It should not become the runtime planner.

```text
Roslyn
 ↓
Semantic descriptors
 ↓
Runtime planner
```

The runtime still needs dynamic planning because user intent is not known at compile time.

## Architectural rules

1. Inner layers do not reference outer transports.
2. AI providers do not become domain dependencies.
3. Database-specific concerns stay behind execution-provider boundaries.
4. Domain actions are explicit.
5. Arbitrary method invocation is never an agent capability.
6. Policy is part of execution planning.
7. Mutations are previewable.
8. Important mutations are verifiable.
9. Evidence is produced by the execution system.
10. New integrations should prefer adapters over new core abstractions.

## Historical documentation

The former GraphQL/source-generator architecture remains documented under the older section names for historical context. It should not be treated as the current product boundary.

See `archive/` for historical Graphgine implementation.
