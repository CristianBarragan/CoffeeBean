# Policy-Aware Planning

Foundgine treats authorization as semantic information that survives into the
canonical plan. The planner and optimizer never replace execution-time policy
checks with discovery data.

## Phase 8 boundary

The first policy-aware optimization is deliberately conservative:

```text
Semantic request
    ↓
Resolve
    ↓
Authorize
    ↓
Semantic plan
    ↓
Policy-aware normalization
    ↓
Provider lowering
```

Authorization predicates are normalized into a deterministic structural form.
Equivalent `AND`/`OR` expressions are flattened, duplicate terms are removed,
and commutative operands are ordered. Double negation is eliminated.

This improves plan fingerprints and provider-plan cache reuse without changing
policy meaning.

## What the optimizer does not do

The optimizer does not:

- grant authorization;
- infer new permissions;
- evaluate user or resource context;
- remove authorization predicates because a transport claims they are safe;
- translate authorization directly into SQL;
- merge predicates across different semantic resources without proof;
- bypass the semantic authorization boundary.

Provider-specific predicate lowering remains the responsibility of the provider
compiler.

## Why this matters

Without normalization, these two policies can produce different plan identities:

```text
A AND B
B AND A
```

even though they are semantically equivalent. Canonicalization gives them the
same deterministic representation and therefore the same plan fingerprint.

That matters for:

- context-safe provider plan caching;
- execution receipts;
- approval stability;
- deterministic AOT artifacts;
- semantic-plan comparison;
- future cost-based optimization.

## Next optimization layer

The next policy-aware optimization should be predicate placement analysis:

```text
authorization predicate
        ↓
semantic dependency analysis
        ↓
early safe evaluation point
        ↓
physical provider lowering
```

That should only be introduced once Foundgine can prove that moving a predicate
earlier preserves relationship, cardinality, null, aggregation, and pagination
semantics.
