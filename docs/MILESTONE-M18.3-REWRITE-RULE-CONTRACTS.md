# M18.3 — Rewrite Rule Contracts

## Purpose

M18.3 freezes the contract for individual provider-neutral semantic plan rewrites.
An optimizer rule is no longer an opaque implementation detail: it declares its
preconditions, security obligations, transformation, and estimated cost impact.

## Rule contract

Each `IPlanRewriteRule` exposes:

- `Name` — stable, auditable rule identifier.
- `Preconditions` — human-readable conditions describing when the rule may apply.
- `SecurityObligations` — invariants the rule explicitly promises not to weaken.
- `CostImpact` — estimated relative planning/execution impact.
- `CanApply` — executable precondition check.
- `Apply` — provider-neutral transformation.

## Proof boundary

A rule cannot become an accepted optimizer transformation merely because it returns
a syntactically valid plan. `SemanticPlanOptimizer.ApplyRule` immediately evaluates:

1. `SecurityPreservationProof`.
2. `SemanticEquivalenceProof`.

If either proof fails, the rule application is rejected and the candidate plan is
not accepted by the optimizer.

The security and semantic proof layers therefore remain independent of individual
rule implementations.

## Current rule

The first concrete rule is:

`authorization.canonicalization`

It normalizes equivalent authorization boolean expressions by removing duplicate
terms, collapsing single operands, eliminating double negation, flattening boolean
associativity, and applying deterministic operand ordering.

The rule does not authorize anything, invent policy, or lower to SQL.

## Cost model

`CostImpact` is intentionally simple in M18.3. It is metadata for the planner and
is not yet a full cost estimator. Future milestones can replace this scalar with a
provider-aware cost model without changing the rule contract.

## Security model

Rule declarations are obligations, not proof. A rule claiming `authorization.runtime`
does not make runtime authorization correct by itself. The independently generated
security proof and downstream provider conformance remain authoritative gates.

## Result

The planner now has an extensible, auditable transformation boundary:

```text
Semantic Plan
    ↓
Candidate Rule
    ↓
Preconditions
    ↓
Transformation
    ↓
Semantic Equivalence Proof
    ↓
Security Preservation Proof
    ↓
Accept / Reject
```

This provides the foundation for future predicate pushdown, join reordering,
projection pruning, relationship optimization, aggregate rewrites, and
provider-specific optimization while keeping each transformation inside the
same proof boundary.
