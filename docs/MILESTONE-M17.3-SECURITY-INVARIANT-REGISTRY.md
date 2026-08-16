# M17.3 — Security Invariant Registry

## Purpose

M17.3 makes security guarantees a first-class part of Foundgine's semantic contract.
Security is no longer represented only by individual authorization checks and adversarial tests. A capability can now carry a machine-readable set of security invariants that downstream planning and provider layers are required to preserve.

## Architecture

```text
Untrusted intent
      ↓
Semantic capability
      ↓
Required security invariants
      ↓
Contract validation
      ↓
Semantic plan
      ↓
Provider plan
      ↓
Execution
      ↓
Evidence / receipt
```

The registry is provider-neutral. It does not grant authorization and does not claim that the presence of an invariant proves that a provider has implemented it correctly. It defines the required contract and provides a structural validation gate.

## Canonical invariants

- `authorization.required`
- `authorization.runtime`
- `tenant.isolation`
- `visibility.field`
- `visibility.relationship`
- `execution.parameterized-values`
- `planning.cache-context-isolation`
- `mutation.atomic`
- `mutation.idempotency`
- `mutation.replay-protection`
- `evidence.audit`
- `evidence.execution-receipt`

## Important distinction

The registry separates **requirements** from **enforcement**.

For example, `tenant.isolation` can be required by a capability, but Foundgine must still verify that the selected provider plan and execution context preserve it. A string in metadata is not a security boundary by itself.

Likewise, `authorization.runtime` means that authorization must be evaluated against current execution context. It does not permit an agent or serialized intent to supply its own authorization decision.

## Generic capability defaults

Generic semantic capabilities receive a minimum invariant set including:

- authorization required
- parameterized values
- field visibility when fields are exposed
- relationship visibility when relationships are exposed
- runtime authorization for mutations and conditionally authorized operations

High-assurance domain capabilities can explicitly declare stronger requirements such as atomicity, idempotency, replay protection, tenant isolation, audit and execution evidence.

## Contract validation

`SecurityInvariantContractValidator` performs structural validation before provider planning. It fails closed when:

- an unknown invariant identifier is referenced;
- a mutating capability omits authorization;
- a mutating capability omits runtime authorization;
- exposed fields lack field-visibility protection; or
- exposed relationships lack relationship-visibility protection.

This is intentionally a contract gate rather than a runtime authorization engine.

## What M17.3 proves

M17.3 establishes that security requirements have a stable, machine-readable vocabulary and can travel with semantic capabilities independently of transport and provider implementation.

It also creates a foundation for future plan-level proof checking: a provider plan can be required to demonstrate preservation of the invariants declared by the capability.

## What M17.3 does not prove

It does not prove that every provider implementation preserves every invariant. That remains an execution-provider responsibility and must be tested at the actual physical boundary.

It also does not make model interpretation trustworthy. M17.2 remains responsible for treating model output as untrusted input.

## Next gate

The natural follow-on is **M17.4 — Plan-Level Invariant Proof**.

M17.4 should attach the required invariant set to the semantic plan and require provider compilation to return a preservation result. A provider should be unable to produce an executable plan unless every required invariant is either preserved or explicitly rejected.
