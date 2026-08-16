# M18.9 — Projection Pruning

M18.9 introduces the first conservative projection optimization in Foundgine.

## What it does

The `projection.pruning` rewrite removes redundant duplicate field entries while preserving their first occurrence and therefore preserving the requested field order.

The rule also exposes `ProjectionPruningRequirements`, which identifies root fields required by:

- the output projection;
- root-level field filters; and
- root-level ordering terms.

Relationship and aggregate requirements remain conservative and are not used to remove fields across relationship boundaries.

## Why the rule is deliberately conservative

The current `SemanticPlanNode.Fields` collection represents the requested output projection and does not yet distinguish output fields from internal working fields introduced for filters, joins, authorization, pagination, or provider execution.

Therefore M18.9 does **not** remove a unique requested field merely because it is not referenced by a predicate. Full dead-field pruning requires an explicit requested-vs-working projection model.

## Proof obligations

Every accepted rewrite continues through the existing proof chain:

1. semantic equivalence;
2. security preservation;
3. provider-aware cost selection when configured.

The semantic equivalence fingerprint treats duplicate projection entries as redundant while preserving unique field identity.

## Security boundary

The rule cannot remove fields merely because they appear unrelated to security. Field visibility, authorization, relationship visibility, filtering, and ordering remain part of the semantic plan contract.

## What this milestone does not prove

M18.9 does not claim:

- arbitrary dead-column elimination;
- relationship-aware projection pruning;
- provider-specific index selection;
- reduced PostgreSQL execution cost for every query.

Those require richer projection dependency metadata and provider statistics.
