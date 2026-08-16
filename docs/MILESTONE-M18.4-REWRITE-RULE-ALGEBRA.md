# Milestone M18.4 — Rewrite Rule Algebra + Composition

## Purpose

M18.4 freezes the composition semantics for Foundgine's provider-neutral plan rewrite rules.

A rewrite is no longer an isolated transformation. Rules form a deterministic algebra with explicit ordering, conflict, idempotence, cost and termination semantics.

## Composition model

```text
Plan
  ↓
Rule selection
  ↓
Dependency / ordering constraints
  ↓
Conflict validation
  ↓
Deterministic rule order
  ↓
Rewrite
  ↓
Semantic equivalence proof
  ↓
Security preservation proof
  ↓
Cost accumulation
  ↓
Next rule / fixed point
```

## Rule contract

Each `IPlanRewriteRule` may declare:

- `MustRunAfter`
- `MustRunBefore`
- `ConflictsWith`
- `IsIdempotent`
- `Priority`
- preconditions
- security obligations
- estimated cost impact

Existing rules remain source-compatible because the composition properties have conservative defaults.

## Ordering

Rules are topologically ordered from explicit dependencies. Priority and name provide deterministic tie-breaking between otherwise independent rules.

Unknown dependencies and ordering cycles fail closed during composer construction.

## Conflicts

Two rules may declare that they cannot coexist in the same composition set. Mutual conflicts are rejected before optimization begins. This prevents incompatible transformations from being selected accidentally.

## Idempotence

Idempotent rules are applied at most once in a composition. Non-idempotent rules may participate in repeated passes, subject to the composition budgets and plan-fingerprint cycle detection.

## Termination

The composer has explicit limits for:

- maximum rule applications
- maximum distinct plan visits

A repeated plan fingerprint is treated as a rewrite cycle and fails closed. This is preferable to silently returning a potentially unstable optimization result.

## Proof accumulation

Every accepted application independently produces:

- `SemanticEquivalenceProof`
- `SecurityPreservationProof`

The composition result retains every application and the cumulative cost impact. A later rule therefore cannot erase evidence from an earlier transformation.

## Cost

Rule costs are accumulated in `TotalCostImpact`. M18.4 does not yet define a global cost model or choose between competing equivalent plans. It establishes the algebra needed for that future planner stage.

## What M18.4 proves

- rewrite rules can be composed deterministically
- dependencies are explicit
- conflicts are detected
- idempotence is represented
- cycles are rejected
- composition is bounded
- proof obligations remain attached to each transformation
- cumulative cost is observable

## What it does not prove

M18.4 does not prove that every optimizer rule is globally optimal, nor does it prove provider-specific execution equivalence. Those remain later planner/provider concerns.
