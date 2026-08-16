# M18.10 — Relationship Traversal Optimization

M18.10 introduces the provider-neutral contract for cardinality-aware relationship traversal optimization.

## What changed

`SemanticPlanNode` now carries optional relationship cardinality metadata and a `RelationshipTraversalMode` optimization hint:

- `Default`
- `SingleHop` for one-to-one relationships
- `SetBased` for collection relationships

`RelationshipTraversalOptimizationRule` derives the hint only when cardinality metadata is available.

## Safety boundary

The rule does **not** change:

- relationship identity
- graph topology
- selected fields
- filters
- authorization predicates
- ordering
- pagination
- mutation semantics

It therefore changes execution strategy metadata, not semantic meaning.

The hint participates in the execution/plan fingerprint because a provider may compile different physical strategies from it, but it is intentionally excluded from semantic equivalence.

## Important limitation

M18.10 does not claim that every provider must implement a different physical traversal. A provider may ignore the hint when its execution model cannot safely exploit it.

The PostgreSQL compiler currently retains its existing relationship SQL semantics. This milestone establishes the planner-to-provider contract needed for later provider-specific traversal strategies without weakening the semantic layer.

## Proof chain

```text
Relationship metadata
    ↓
Cardinality-aware rule
    ↓
Traversal strategy hint
    ↓
Semantic equivalence proof
    ↓
Security preservation proof
    ↓
Provider-aware cost
    ↓
Provider may exploit hint
```

## What this proves

- relationship cardinality can influence planning without becoming storage-specific
- traversal strategy is distinct from semantic meaning
- missing cardinality fails closed by leaving the plan unchanged
- physical strategy changes can participate in plan-cache identity
- semantic and security proof boundaries remain intact

## What this does not prove

It does not prove that a provider's physical traversal implementation is optimal or correct merely because it accepts a hint. Provider-specific execution must still establish its own conformance and integration tests.
