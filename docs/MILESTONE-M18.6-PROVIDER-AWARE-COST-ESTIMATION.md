# M18.6 — Provider-Aware Cost Estimation

## Purpose

M18.6 adds the provider-aware cost boundary to the Foundgine rewrite algebra.

M18.5 established provider-neutral rewrite cost and benefit selection. M18.6 allows a provider to contribute an execution-cost estimate without taking ownership of semantic correctness or security.

## Architecture

```text
Semantic Plan
      ↓
Candidate rewrite
      ↓
Provider cost estimator
      ↓
Provider execution estimate
      ↓
Rule selection
      ↓
Rewrite
      ↓
Semantic equivalence proof
      ↓
Security preservation proof
      ↓
Executable plan
```

The provider estimate is advisory. It may influence which valid candidate wins, but it cannot make an invalid transformation valid.

## Provider boundary

`IProviderCostEstimator` lives in `Foundgine.Planning` as a provider-neutral contract:

- provider identity
- candidate semantic plan
- rewrite rule
- execution-cost estimate
- optional row estimate
- confidence

The provider does not receive SQL text at this stage.

## SQL implementation

`Foundgine.Sql.SqlCostEstimator` supplies the first concrete provider model.

The initial model is intentionally heuristic. It estimates cost from semantic shape:

- scan base cost
- selected field count
- relationship traversal
- filters
- relationship filters
- aggregate filters
- ordering terms
- limits
- offsets
- cursor pagination
- child plan nodes

This is **not** presented as a database optimizer or as a replacement for PostgreSQL statistics. It is the stable provider boundary into which statistics-backed estimates can later be introduced.

## Selection rule

The combined provider-aware score is:

```text
benefit
────────────────────────────────────
1 + rewriteCost + providerCost × weight
```

The score is deterministic. Existing rule priority and ordinal-name tie breaking remain in force.

## Security boundary

Provider cost information cannot bypass:

- semantic equivalence
- security preservation
- rule preconditions
- rewrite dependencies
- conflicts
- idempotence
- termination limits

A provider may say a transformation is extremely cheap. If the transformation changes meaning or weakens security, it is still rejected.

## Why this matters

Foundgine can now separate four concerns:

```text
Semantic meaning
       ↓
Security guarantees
       ↓
Provider capability
       ↓
Provider execution cost
```

This permits provider-aware optimization without moving provider-specific physical concepts into the semantic planner.

## What M18.6 does not claim

It does not claim:

- exact database cost prediction
- PostgreSQL planner equivalence
- statistics accuracy
- cardinality estimation correctness
- globally optimal plan selection

Those are future provider-specific capabilities.

## Next

M18.7 should introduce **costed candidate-plan comparison and statistics provenance**, allowing a provider to identify where an estimate came from and how stale it is before the planner accepts a cost-driven rewrite.
