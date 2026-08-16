# M15 — Semantic Security Boundary

This milestone makes the security criticism concrete without claiming that Foundgine can make an incorrect business policy correct.

## Implemented

- adversarial provider-independent authorization tests
- conditional tenant predicate preservation
- hidden-field capability suppression
- unauthorized relationship traversal suppression
- capability side-effect/idempotency assertions
- stateful PostgreSQL `QUERY -> MUTATION -> QUERY -> MUTATION` integration proof
- CI separation: unit tests first, PostgreSQL integration tests second
- lean AI sample retained as a boundary demonstration
- CoffeeBeanery composite deep-dive sample

## Acceptance criteria

1. Unit tests never execute the PostgreSQL E2E project.
2. Integration tests cannot start until unit tests pass.
3. Integration tests use a real PostgreSQL 17 service.
4. Stateful integration proves that a query sees a prior mutation in the same transaction.
5. A later mutation is asserted to affect exactly the intended graph.
6. Authorization predicates survive semantic capability discovery.
7. Unauthorized fields and relationships are not exposed as capabilities.

## Deliberately not claimed

Foundgine still does not own authentication, deployment security, secrets, transport security, database permissions, or the correctness of the business policy itself.

The next security gate should add provider-level adversarial execution, resource limits, idempotency/replay tests and the high-assurance `TransferFunds` mutation scenario.
