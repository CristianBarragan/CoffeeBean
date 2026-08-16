# M18.5 — Rewrite Cost Model + Rule Selection

## Purpose

M18.5 turns rewrite composition from a deterministic sequence into a deterministic **selection process**.

M18.3 defined individual rewrite-rule contracts. M18.4 defined how those rules compose and terminate. M18.5 adds the missing decision layer: when several rules are applicable, Foundgine can rank them using an explicit, provider-neutral cost/benefit model.

## Selection model

For each currently applicable rule:

```text
benefit estimate
      /
     /  (1 + rewrite cost)
    ↓
selection score
```

The selector then applies deterministic tie-breakers:

1. higher score
2. higher rule priority
3. ordinal rule name

A minimum score can be configured to prevent low-value rewrites from being selected.

## New contracts

### `RewriteCost`

Represents estimated rewrite work. It is deliberately provider-neutral and must be finite and non-negative.

### `RewriteBenefit`

Represents estimated execution benefit. It is also finite and non-negative.

### `RewriteScore`

Provides the deterministic normalized score used for selection.

### `RuleSelectionPolicy`

Controls whether benefit is preferred and whether rewrite cost is penalized. It also provides a configurable minimum score.

### `RewriteRuleSelector`

Selects only currently applicable rules and returns an auditable `RewriteRuleCandidate`.

## Rule contract

`IPlanRewriteRule` now exposes:

```csharp
double BenefitEstimate => 0d;
```

The default keeps existing rules source-compatible. Rules that do not have a meaningful optimization benefit can remain at zero.

The existing `CostImpact` remains the accumulated execution/planning cost contribution recorded by composition.

## Composition behavior

M18.5 preserves all M18.4 safety rules:

- dependency ordering
- `MustRunAfter`
- `MustRunBefore`
- conflicts
- idempotence
- cycle detection
- maximum rule applications
- maximum plan visits
- semantic-equivalence proof
- security-preservation proof

Selection happens **only among rules that are currently eligible**. It cannot bypass ordering or conflict constraints.

Every selected candidate is retained in `RewriteRuleCompositionResult.SelectionHistory`, making rule selection auditable.

## Important distinction

The M18.5 cost model is an **estimate**, not a database execution planner.

It does not yet know PostgreSQL cardinalities, index statistics, network latency, provider-specific join costs, or runtime telemetry.

Those belong to later provider-aware cost estimation.

The purpose of M18.5 is to freeze the algebra and contract for making such estimates safely.

## Security boundary

Rule selection does not weaken the security proof system.

The pipeline remains:

```text
candidate rules
    ↓
preconditions
    ↓
security obligations
    ↓
semantic rewrite
    ↓
semantic equivalence proof
    ↓
security preservation proof
    ↓
accept
```

A high-scoring rule that fails either proof is rejected.

## Tests

M18.5 adds coverage for:

- higher-benefit selection
- cost-aware selection
- deterministic tie-breaking
- selection-history recording
- continued proof enforcement

## What M18.5 does not claim

It does not claim:

- globally optimal plans
- database-specific cost accuracy
- runtime adaptive optimization
- cardinality estimation
- physical index selection
- benchmark-derived cost calibration

Those require provider-specific information and are intentionally deferred.
