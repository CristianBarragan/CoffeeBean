- [x] M0.9 — Execution Boundary Freeze: execution consumes provider-independent plans and semantic models without a direct metadata dependency.

# Ground-Up Roadmap

- [x] M1 — Semantic Foundation: IDs, metadata, semantic node/edge/graph.
- [x] M2 — Resolution: semantic request -> request graph.
- [x] M3 — Authorization: request graph -> authorized graph.
- [x] M4 — Planning: authorized graph -> provider-independent execution plan.
- [x] M5 — SQL execution: execution plan -> SQL provider plan -> SQLite.
- [x] M6 — AOT: domain model -> generated metadata.
- [x] M7 — GraphQL adapter: Hot Chocolate -> SemanticRequest.

## M0.9 boundary

Execution consumes `ExecutionPlan` and `SemanticModel`, produces semantic execution results, and does not directly depend on `Foundgine.Metadata`. Provider-specific physical plans remain opaque to the core execution layer.

## M7 boundary

GraphQL is an adapter, not the engine. The active GraphQL project references only the semantic/metadata contracts plus the Hot Chocolate language parser. It does not reference planning, execution, or SQL.

The first M7 proof supports a single query operation with one root field, scalar selections, relationship selections, and transparent inline fragments. Arguments, variables, aliases, directives, named fragments, mutations, filtering, ordering, pagination, subscriptions, and Relay wrappers remain deferred because the current SemanticRequest does not represent those concerns.

## First complete acceptance path

```text
GraphQL
   ↓
SemanticRequest
   ↓
Resolve
   ↓
Authorize
   ↓
ExecutionPlan
   ↓
ProviderPlan
   ↓
SQLite
   ↓
ExecutionResult
```

Customer -> Account -> Transaction is the proof domain.

- M0.10 — Provider Boundary Freeze: complete


## M0.15 — AOT Boundary Freeze

AOT generation is frozen against the M1–M5 contracts. Relationship generation preserves source/target key direction for both FK ownership patterns.

## M0.9 — Planning Dependency Audit

Planning's dependency on Semantics is intentional and frozen. Planning consumes
semantic graphs, query options, and semantic filter expressions; duplicating those
contracts would create a second semantic vocabulary. Planning remains independent
of concrete metadata, SQL, GraphQL, and provider execution types.

## M11 — JSON Structured Intent Adapter

Status: complete.

A thin JSON input adapter now converts a small wire format into `ReadIntent`, with an SQLite end-to-end acceptance test proving reuse of the existing semantic pipeline.

## M12 — Untrusted Intent Safety

- JSON intent treated as untrusted input
- Resolution rejects unknown semantic concepts
- Authorization cannot be bypassed by the intent producer
- Root authorization failures stop the pipeline
- JSON parser has configurable depth/node bounds
