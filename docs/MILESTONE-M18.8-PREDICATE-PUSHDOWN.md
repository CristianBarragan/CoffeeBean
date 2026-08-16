# M18.8 — Predicate Pushdown

## Purpose

M18.8 introduces the first concrete optimizer transformation built on the M18 proof and cost architecture.

The rule is deliberately conservative. It performs provider-neutral Boolean predicate pushdown:

```text
(A OR B) AND C
        ↓
(A AND C) OR (B AND C)
```

This exposes the conjunct `C` to every disjunct, which can allow a physical provider to exploit more selective predicates when compiling the plan.

## What the rule does not do

M18.8 does **not** push predicates across:

- relationship boundaries
- authorization boundaries
- pagination
- ordering
- cardinality-changing operations
- provider-specific storage operations

Those transformations require explicit relationship/cardinality contracts and belong to later milestones.

## Proof boundary

Every application continues through:

```text
candidate rule
    ↓
semantic equivalence proof
    ↓
security preservation proof
    ↓
cost/provider selection
    ↓
accepted rewrite
```

The semantic-equivalence fingerprint now recognizes bounded Boolean distributivity using a canonical DNF representation. DNF expansion is bounded to prevent optimizer work from becoming unbounded.

## Security

Predicate rewriting cannot remove the plan's required security invariants. The rule does not rewrite authorization predicates; authorization remains a separate execution-boundary contract.

## Cost

The rule has an explicit rewrite cost and benefit. Expansion is capped at 16 terms. Provider-aware selection may reject it when its estimated execution cost is not justified by the expected benefit.

## Status

Implemented as `predicate.pushdown.disjunction` with focused planner tests.

The implementation is provider-neutral and intentionally stops short of physical SQL predicate placement.
