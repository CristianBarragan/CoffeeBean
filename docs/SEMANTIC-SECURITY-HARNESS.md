# Semantic Security Harness

The semantic security harness is a deterministic adversarial test layer for Foundgine's semantic-to-planning boundary.

## Purpose

The harness does not attempt to prove that every provider is secure. It proves a narrower and more important invariant:

> A request that crosses the semantic authorization boundary must not cause denied semantic entities, fields, relationships, or authorization predicates to escape into the canonical plan.

The harness deliberately runs without SQL, GraphQL, EF Core, Hot Chocolate, or a database provider.

## Invariants

The fuzz suite checks that:

1. denied root entities reject the request;
2. denied entities never appear in an authorized plan;
3. denied fields never appear in a plan;
4. denied relationship traversals never appear in a plan;
5. authorization predicates survive authorization and planning;
6. generated cases are reproducible from a fixed seed;
7. the semantic plan remains the security boundary before physical lowering.

## Deterministic fuzzing

The first harness uses a fixed seed and generates 1,000 hostile semantic graphs. This is intentional: failures can be reproduced locally by reporting the seed and case index.

The harness is designed to evolve into property-based testing later, but the initial invariant suite stays dependency-light and provider-independent.

## Security boundary

```text
Untrusted intent
      |
      v
Semantic resolution
      |
      v
Semantic authorization  <--- security boundary
      |
      v
Authorized Semantic IR
      |
      v
Semantic planner
      |
      v
Canonical Semantic Plan
      |
      v
Physical/provider lowering
```

The optimizer and providers must not be allowed to become alternate authorization authorities.
