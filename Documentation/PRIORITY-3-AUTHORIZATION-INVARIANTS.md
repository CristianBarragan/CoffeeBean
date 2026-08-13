# P0.3 — Authorization Invariants

Authorization is a semantic boundary, not a provider feature.

The following invariants are part of the Foundgine contract.

## Invariant 1 — denied root entities fail before planning

A denied root entity produces `SemanticAuthorizationException`. No execution plan or provider plan is produced.

```text
Intent → Resolution → Authorization ✗
                         │
                         └── no Plan / no Provider
```

## Invariant 2 — denied fields are removed before planning

A field denied by the semantic policy is absent from the authorized graph and therefore absent from the execution plan and provider projection.

A provider must never receive a field that semantic authorization removed.

## Invariant 3 — denied relationships remove the reachable subtree

A denied relationship cannot be represented as an executable traversal. Its child node and descendants become unreachable and are removed from the authorized graph.

## Invariant 4 — conditional authorization survives planning

A conditional authorization predicate is preserved on the semantic node and copied into the provider-independent execution plan.

The planner does not evaluate, rewrite, or discard the predicate.

## Invariant 5 — providers must enforce the predicate

Provider lowering is responsible for enforcing an already-authorized predicate using the current execution context.

For SQL this becomes a parameterized `WHERE` predicate. For the in-memory proof provider it is evaluated against the row and runtime context.

## Invariant 6 — authorization context is runtime state

Context values such as `user.TenantId` are not embedded in the plan fingerprint. The predicate shape may be cached, but the runtime value is supplied when the provider executes.

## Invariant 7 — cache lookup never replaces authorization

The pipeline is deliberately ordered:

```text
Intent
  ↓
Resolution
  ↓
Authorization
  ↓
Planning
  ↓
Cache lookup / provider compilation
  ↓
Execution
```

A denied request therefore cannot obtain a provider plan from the cache.

## Invariant 8 — capability discovery is not authorization

Capability discovery describes policy state. It does not authorize a request. Every request still passes through `SemanticAuthorizer` before planning.

## What is deliberately not promised

These invariants do not claim that authentication, database permissions, transport security, rate limiting, or infrastructure isolation are provided by Foundgine. They define the authorization boundary inside the semantic execution pipeline.
