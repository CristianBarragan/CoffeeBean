# Extensibility

Foundgine is extensible through explicit boundaries rather than inheritance-heavy framework hooks.

## Main extension points

### Semantic

Implement/configure:

```text
SearchCapability
ActionDescriptor
PolicyDescriptor
CandidateSource
```

### Planning

Extend the provider-neutral intent/plan model when a real scenario requires a new logical operation.

Do not add provider-specific nodes to the logical planner.

### Execution

Implement `IExecutionProvider` for a new execution target.

The provider owns:

```text
ProviderPlan → external system
```

## Adapter rule

A new integration should normally be an adapter.

Examples:

```text
MCP adapter
GraphQL adapter
ASP.NET Core adapter
PostgreSQL provider
retrieval provider
```

## Avoid extension by leakage

Do not extend Foundgine by adding:

```text
SQLite-specific logic to Metadata
LLM-specific logic to Planning
GraphQL-specific types to Semantic
provider implementation details to Execution.Contracts
```

That is architectural erosion, not extensibility.
