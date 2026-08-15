# Capability Contract v2

Capability contracts are the canonical machine-readable description of what a semantic application surface can do.

They are generated from the same semantic model and authorization policy used by execution. They are descriptive and never replace execution-time authorization.

## Operations

A capability now identifies a semantic operation:

- `read`
- `write`
- `create`
- `update`
- `delete`
- `upsert`
- `traverse`

The broad `write` capability remains for compatibility. The action capabilities make mutation intent explicit and provide a stable foundation for agents, MCP and approval workflows.

## Constraints

Constraints describe semantic requirements without embedding provider-specific SQL or storage details.

Examples:

- writable fields
- target selection
- conflict key
- execution-time authorization

A constraint is descriptive. The planner remains responsible for enforcing it.

## Effects

Effects describe the meaning of an operation, not its physical implementation.

For example, `order.refund` can eventually declare payment reversal and audit creation without exposing SQL, HTTP calls or provider mechanics.

## Idempotency

The contract exposes a conservative `IsIdempotent` hint. It is not a guarantee of distributed idempotency. Runtime execution must still use explicit idempotency semantics where retries can occur.

## Design rule

```text
Semantic Model
     ↓
Capability Contract
     ↓
Intent
     ↓
Authorization
     ↓
Plan
     ↓
Execution
```

AI, MCP, GraphQL and other transports consume the contract; they do not define their own competing capability model.
