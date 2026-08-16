# M18.1 — Security-Preserving Plan Rewriting

## Purpose

M18.1 establishes that provider-neutral semantic optimization cannot weaken the security contract attached to a plan.

The optimization boundary is:

```text
Authorized Semantic Plan
        ↓
Semantic Rewrite / Optimization
        ↓
Rewritten Semantic Plan
        ↓
Security Preservation Proof
```

A rewrite is accepted only when every security invariant required by the input plan is still required by the rewritten plan.

## Core invariant

For a rewrite `P -> P'`:

```text
RequiredSecurityInvariants(P)
    ⊆
RequiredSecurityInvariants(P')
```

The current implementation intentionally requires equality of the invariant sets. This is conservative: a rewrite cannot silently add or remove the security contract without an explicit planning decision.

## SecurityPreservationProof

`SecurityPreservationProof` records:

- invariants before rewriting
- invariants after rewriting
- missing invariants
- before-plan fingerprint
- after-plan fingerprint
- satisfaction status

Unknown invariant identifiers are rejected.

If an invariant is removed, the rewrite fails closed with `InvalidOperationException`.

## What M18.1 proves

- authorization predicate normalization preserves security requirements
- optimization retains plan-level security invariants
- security requirements remain part of deterministic plan fingerprints
- a rewrite that drops an invariant cannot be accepted through the normal proof gate

## What it does not prove

M18.1 does not prove that every optimizer transformation is semantically equivalent at the data level, nor that a provider implementation is correct. It proves the narrower and necessary property that semantic optimization cannot silently weaken the declared security contract.

Provider-specific preservation remains the responsibility of M17.5–M17.7 conformance gates.

## M18.2 — Semantic Equivalence Proof

M18.2 extends the rewrite boundary from security preservation to provider-neutral semantic meaning.

The optimizer now produces both:

```text
SecurityPreservationProof
SemanticEquivalenceProof
```

A semantic rewrite is accepted only when the canonical semantic representation of the source and rewritten plans is identical.

`SemanticEquivalenceFingerprint` canonicalizes only transformations explicitly defined as equivalent, including commutative authorization operands and structurally equivalent authorization normalization. Material changes to entity scope, fields, relationships, predicates, ordering, pagination, or the security contract remain distinguishable.

This is intentionally a canonical-representation proof rather than a claim of formal theorem proving or provider correctness.
