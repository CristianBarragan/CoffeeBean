# M17.4 — Plan-Level Security Invariant Proof

## Purpose

M17.4 moves Foundgine security from a capability declaration into the executable planning boundary.

The pipeline is now:

```text
Semantic capability / authorized request
        ↓
Required security invariants
        ↓
Semantic plan
        ↓
Execution IR
        ↓
Provider compilation
        ↓
Provider preservation attestation
        ↓
Security proof gate
        ↓
Executable provider plan
```

A provider plan cannot cross the execution boundary when the provider compiler does not declare preservation of every invariant required by the current plan.

## What is new

### Plan-level requirements

`SemanticPlan` carries `RequiredSecurityInvariants`.

`SecurityInvariantPlanRequirements` derives minimum requirements from the authorized plan shape and preserves any capability-specific requirements already attached to the plan.

The baseline includes:

- authorization required
- parameterized values
- provider-plan cache context isolation
- field visibility when fields are selected
- relationship visibility when relationships are traversed
- runtime authorization when an authorization predicate is present

### Execution IR propagation

`ExecutionIR` carries the exact invariant requirements into provider compilation.

This prevents the provider compiler from receiving only the query shape while silently losing the security contract.

### Provider preservation contract

Providers that participate in the proof gate implement `ISecurityInvariantProviderCompiler` and declare the canonical invariants they preserve.

The gate computes:

```text
required - preserved = missing
```

If `missing` is non-empty, execution is rejected.

### Provider plan attestation

`ProviderPlan.SecurityProof` records:

- provider
- required invariants
- preserved invariants
- missing invariants
- satisfaction state

This makes the security result inspectable alongside the physical plan.

## Cache and approval safety

Security requirements are included in the semantic plan fingerprint and shape key. Changing the required security contract therefore invalidates an otherwise identical cached/approved plan.

## Important limitation

This is a **contract proof / provider attestation**, not a formal mathematical proof of provider implementation correctness.

The registry can establish that:

> the semantic plan requires X, and the selected provider declares that it preserves X.

It cannot establish by itself that a provider implementation is bug-free.

Provider-specific structural tests and integration tests remain necessary.

## Security progression

```text
M17
  hostile agent boundary

M17.1
  black-box pipeline attacks

M17.2
  model-output replay

M17.3
  machine-readable security invariants

M17.4
  plan-level invariant preservation gate
```

The architecture now makes security requirements part of the planning/execution contract rather than only a test concern.
