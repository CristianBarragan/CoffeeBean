# M13 — Multi-Producer Equivalence

## Goal

Prove that independent intent producers express the same request through the same provider-neutral semantic contract.

M13 compares the JSON structured-intent adapter with the Hot Chocolate GraphQL adapter.

## Boundary

```text
GraphQL ─────────┐
                 ├──> SemanticRequest ──> Resolution ──> Authorization ──> Planning ──> Execution
JSON ───────────┘
```

Neither producer is allowed to define a second semantic representation after adaptation.

## Acceptance

For equivalent requests, the adapters must produce equivalent:

- root `EntityId`
- field/relationship selections
- nested selections
- filter field identity, operator, and value
- ordering field identity, direction, aggregate, and relationship path
- limit, offset, and cursor

The comparison is made on stable semantic identities, not GraphQL names or JSON strings.

## Why this matters

This is the first direct proof that Foundgine is a shared semantic substrate rather than a collection of independent protocol implementations.

Adding another producer should add translation code at the boundary, not another query/planning/execution pipeline.
