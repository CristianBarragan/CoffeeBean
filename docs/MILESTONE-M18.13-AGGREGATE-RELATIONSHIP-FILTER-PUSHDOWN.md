# M18.13 — Aggregate Pushdown + Relationship Filter Interaction

M18.13 adds the first relationship-aware aggregate rewrite that changes the **inside of an aggregate subquery** while preserving the semantic result.

## Transformation

The optimizer recognizes the proven-equivalent shape:

```text
COUNT(R) > 0 AND SOME(R, P)
```

and rewrites it to:

```text
COUNT(R WHERE P) > 0
```

The same rule supports `COUNT(R) >= 1` and `COUNT(R) != 0`.

## Why the transformation is safe

For a collection `R` and predicate `P`:

```text
COUNT(R) > 0 AND EXISTS(R WHERE P)
```

is equivalent to:

```text
COUNT(R WHERE P) > 0
```

because the filtered count is positive exactly when at least one related row satisfies `P`.

The rule does not generalize this identity to arbitrary count thresholds.

## Security boundary

The relationship predicate remains inside the same target-entity scope. The rewrite therefore preserves:

- authorization requirements
- runtime authorization
- relationship visibility
- plan-cache context isolation

The predicate is not removed; its evaluation location moves into the aggregate subquery.

## Provider behavior

The semantic aggregate now optionally carries a target-scope predicate. SQL compilation renders it as an additional `AND` condition in the aggregate subquery.

Example:

```sql
SELECT COUNT(*)
FROM "Account" a0
WHERE a0."CustomerId" = c."Id"
  AND a0."Status" = @p0
```

No provider-specific optimization is encoded in the semantic rule itself.

## Deliberate non-goals

M18.13 does **not** rewrite:

- `COUNT > 1`
- arbitrary aggregate thresholds
- `MIN` / `MAX`
- `ALL` quantifiers
- `NONE` quantifiers
- duplicate-sensitive aggregates beyond the proven count-existence identity
- pagination-sensitive relationship operations

Those require additional cardinality/null/quantifier algebra.

## Proof requirements

The normal M18 proof pipeline remains mandatory:

```text
rewrite
  ↓
semantic equivalence
  ↓
security preservation
  ↓
provider capability
  ↓
cost selection
  ↓
execution
```

M18.13 therefore extends optimization capability without creating a bypass around the existing proof architecture.
