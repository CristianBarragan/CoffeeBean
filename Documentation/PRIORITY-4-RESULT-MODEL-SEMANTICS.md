# P1 — Result / Model Semantics

## Contract

Foundgine's execution result is provider-neutral. A provider may return flat rows, but the execution layer reconstructs the semantic result tree from:

- `EntityId`
- `FieldId`
- `RelationshipId`
- execution-plan node identity
- semantic identity values

Provider column names are not part of the canonical result model.

## Projection versus identity

`MaterializedNode.Values` contains only fields selected by the execution plan. The entity identity used to reconstruct topology is retained separately as `IdentityValue`.

This distinction matters when an identity field is used for deduplication but is not requested by the caller.

The materializer must therefore be able to reconstruct:

```text
provider rows
  ↓
semantic identity
  ↓
unique nodes
  ↓
relationship tree
```

without silently adding unrequested fields to the result.

## Result metadata

`MaterializedResult` preserves `ExecutionPageInfo` and `ExecutionEvidence` from the provider result. Materialization must not discard pagination or provenance metadata merely because the rows have been reshaped.

## What this layer does not own

The core result model does not contain GraphQL aliases, JSON property names, SQL column names, or provider-specific objects. Adapters shape the semantic result for their transport.

## Scope

This priority does not introduce a general result-expression AST, aggregate result nodes, or a new serialization framework. Those concerns remain separate until their semantics are required by the execution model.
