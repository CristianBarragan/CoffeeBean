# Foundgine architecture

## Frozen boundary

Foundgine has two related flows.

### Runtime

```text
External API / Adapter
        |
      Resolve
        |
Semantic Request
        |
Semantic Graph
        |
     Authorize
        |
Authorized Semantic Graph
        |
      Planner
        |
 Provider-independent ExecutionPlan
        |
 Provider compiler
        |
   ProviderPlan
        |
     Provider
```

### Compile time

```text
Domain Model
     |
AOT / Metadata Generation
     |
Static Metadata
     |
Semantic Model / Topology
```

## Boundary rules

- `Foundgine.Semantics` knows nothing about GraphQL, SQL, EF, or a provider.
- `Foundgine.Planning` consumes semantic representations and produces a
  provider-independent plan.
- `Foundgine.Execution` exposes the provider boundary; physical plan details
  belong to a provider implementation.
- `Foundgine.Metadata` describes static domain/storage facts.
- `Foundgine.Aot` generates metadata; runtime projects do not depend on the
  generator implementation.
- A semantic graph is not a SQL join graph.
- A provider plan is not a semantic graph.
- GraphQL adapters translate into `SemanticRequest`; they do not become the
  engine.

## V1 relationship

`archive/FoundgineV1` is a reference/proof implementation. It is intentionally
not referenced by the new source tree.

Only concepts with a current architectural justification are ported.
