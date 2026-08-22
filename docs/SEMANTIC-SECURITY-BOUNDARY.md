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

## Provider execution certificate boundary

A compiled provider plan cannot cross the final execution boundary merely because a provider declares that it preserves the required invariants.

The security path is:

```text
Execution IR
    ↓
provider compilation
    ↓
concrete provider-plan conformance evaluation
    ↓
SecurityInvariantProof
    ↓
exact provider-plan + IR binding
    ↓
execution gate
    ↓
provider execution
```

`SecurityInvariantAttestation` is used for public/provider capability evidence. `SecurityInvariantProof` is an execution certificate and is issued only by the security certification gate.

The certificate is bound to:

- the exact provider-plan instance;
- the provider identity;
- a SHA-256 fingerprint of the exact `ExecutionIR` used for certification;
- the complete required invariant set;
- the concrete provider conformance result.

Record-cloning or transplanting a certificate to another provider plan, changing the provider identity, or presenting the certificate with a different `ExecutionIR` fails closed.

Security-critical provider invariants require a concrete `IProviderSecurityConformanceEvaluator`; a provider-declared capability profile is not sufficient execution evidence.
