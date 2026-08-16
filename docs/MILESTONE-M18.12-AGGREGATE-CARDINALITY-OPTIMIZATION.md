# M18.12 — Aggregate / Cardinality-Aware Optimization

## Purpose

M18.12 introduces a conservative aggregate optimization boundary for collection
relationship filters. It does not rewrite aggregate semantics. Instead, it adds
provider-visible physical hints when a `COUNT` predicate is provably equivalent
to an emptiness/non-emptiness test.

## Supported reductions

For non-negative `COUNT`:

- `COUNT > 0` → exists short-circuit
- `COUNT >= 1` → exists short-circuit
- `COUNT != 0` → exists short-circuit
- `COUNT = 0` → empty short-circuit
- `COUNT < 1` → empty short-circuit
- `COUNT <= 0` → empty short-circuit

The rule does not reduce thresholds such as `COUNT > 1`, and it does not
optimize `MIN`/`MAX` yet.

## Why this is a physical hint

The semantic filter remains unchanged. A provider may implement an exists
short-circuit using an existence test or equivalent early termination, but the
provider remains responsible for preserving authorization, tenant isolation,
relationship visibility, and the exact observable result.

The plan therefore distinguishes:

- semantic meaning — unchanged
- physical execution strategy — optimized

## Safety boundaries

The rule is conservative when multiple aggregate predicates on the same node
would require different strategies. In that case it leaves the node unchanged.

It also does not cross relationship, authorization, pagination, ordering, or
cardinality boundaries.

## Proof model

The optimization continues to use the existing M18 proof chain:

```text
aggregate candidate
    ↓
rule preconditions
    ↓
semantic equivalence
    ↓
security preservation
    ↓
provider cost
    ↓
physical execution hint
```

Because the semantic filter is unchanged, the semantic-equivalence fingerprint
remains stable. The execution fingerprint includes the physical strategy so
cache identity remains explicit.

## What M18.12 does not claim

This milestone does not claim that every database can or should implement an
aggregate as `EXISTS`, nor does it claim optimal aggregate execution. Provider
conformance and provider-specific SQL generation remain separate responsibilities.
