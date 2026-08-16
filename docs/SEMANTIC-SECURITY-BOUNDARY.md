# Semantic Security Boundary

Foundgine does not claim to make business authorization automatically correct. It provides a deterministic boundary in which domain authorization rules can be represented, inspected, planned, tested and enforced.

```text
Human / Agent intent
        ↓
interpretation
        ↓
semantic validation
        ↓
authorization
        ↓
plan validation
        ↓
provider execution
```

## What Foundgine owns

- semantic authorization decisions
- field and relationship access
- conditional authorization predicates
- capability discovery
- plan-level authorization invariants
- deterministic execution after authorization

## What it does not magically solve

- authentication
- secrets
- transport security
- deployment security
- database permissions
- correctness of the application's business policy
- correctness of an LLM's interpretation before it reaches the semantic boundary

## Adversarial test categories

The test suite should continuously exercise:

- cross-tenant access
- hidden fields
- unauthorized relationship traversal
- capability escalation
- mutation escalation
- expensive/deep traversal
- replay and idempotency
- plan manipulation

The important assertion is often the **plan**, not only the returned result.
