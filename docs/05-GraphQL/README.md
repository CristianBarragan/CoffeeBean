[Home](../../README.md) → [Documentation](../README.md) → **Historical GraphQL**

# GraphQL

> **Historical / compatibility documentation.**

The earlier Foundgine direction included Graphgine, a GraphQL-oriented product using Hot Chocolate and generated mapping/planning infrastructure.

That work has been moved to `archive/`.

GraphQL is no longer the identity of the active Foundgine platform.

## Current position

If GraphQL is integrated in the future, it should be an outer adapter:

```text
Hot Chocolate
     ↓
Foundgine semantic API
     ↓
Foundgine planning/execution
```

The core platform must not depend on GraphQL.

See [Direction](../00-Direction/README.md) for the current product thesis.
