# Dry Run and Plan Inspection

Foundgine dry-run is an inspection of the canonical authorized semantic plan. It does not execute a provider operation and it does not create a second planning model.

## Pipeline

```text
SemanticRequest
    -> resolve
    -> semantic IR
    -> authorization
    -> SemanticPlan
    -> PlanInspection
```

The inspection exposes:

- the exact plan fingerprint;
- plan nodes and traversal edges;
- selected fields;
- whether authorization was applied to each node;
- conservative effect information.

## Plan identity

`PlanFingerprint` is generated from the complete authorized plan. A future approval mechanism can bind approval to this fingerprint so execution cannot silently substitute a different plan.

## Boundary

Dry-run is not authorization. Authorization still occurs while producing the plan. Dry-run is also not provider simulation: it does not claim that a database, HTTP service, queue, or payment provider has been contacted.

The intended next layer is plan-bound approval followed by exact-plan execution.
