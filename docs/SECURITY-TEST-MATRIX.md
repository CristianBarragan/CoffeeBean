# Semantic Security Test Matrix

| Attack | Boundary | Expected |
|---|---|---|
| Cross-tenant access | Authorization predicate | Rejected / constrained |
| Hidden field access | Field capability | Not advertised / rejected |
| Unauthorized traversal | Relationship capability | Not advertised / rejected |
| Capability escalation | Capability contract | Denied |
| Mutation outside capability | Mutation authorization | Denied |
| Resource exhaustion | Plan/resource limits | Rejected or bounded |
| Replay | Mutation/idempotency | Safe failure |
| Plan manipulation | Plan invariants | Rejected |

The current phase locks the provider-independent semantic boundary. Provider-level adversarial execution remains a separate integration gate.
