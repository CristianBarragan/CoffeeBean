[Home](../../README.md) → [Documentation](../README.md) → **Foundation**

# Foundation

Foundation contains stable, protocol-neutral primitives used by the active Foundgine platform.

It should remain independent of:

- LLM providers
- MCP
- GraphQL
- Hot Chocolate
- database-specific APIs

See the individual project boundaries in [Architecture → Layers](../02-Architecture/Layers.md).

## Current role

Foundation supports the platform rather than defining the entire AI product.

The product semantics will build on top of:

```text
Foundation
 ↓
Metadata
 ↓
Planning
 ↓
Execution
```

## Rule

Do not place AI orchestration, transport models, SQL syntax, or provider-specific behavior in Foundation.
