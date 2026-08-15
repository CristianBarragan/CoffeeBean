# ord_map / RETURNING Invariants

The PostgreSQL batching implementation must preserve logical row identity
through physical SQL.

Required relationship:

```text
logical operation
    ↓
input ordinal
    ↓
INSERT/UPSERT
    ↓
RETURNING
    ↓
ordinal correlation
    ↓
dependent operation
```

## Invariants

1. A physical returned row must carry enough information to identify its logical
   input operation.
2. SQL row order alone is not a correlation mechanism.
3. `ORDER BY` may make output deterministic but does not establish logical
   identity unless the ordering key is itself part of the correlation contract.
4. An ordinal must remain scoped to the operation/group that produced it.
5. A dependent operation must reference a source ordinal through an explicit
   mapping.
6. Missing or duplicate correlation identities are provider failures/fallback
   conditions, not silently recoverable mappings.
7. `RETURNING` must project every value required by downstream references.
8. Conflict/update paths must preserve the same correlation guarantees as
   insert paths.
9. If PostgreSQL cannot preserve the mapping for a physical batch, the compiler
   must fall back to a safe strategy.

## Critical anti-pattern

This is insufficient:

```sql
INSERT ...
RETURNING id;
```

followed by assuming:

```text
returned row #0 == input row #0
returned row #1 == input row #1
```

unless the physical SQL explicitly preserves that relationship.

The safe shape is conceptually:

```text
input ordinal
     +
physical mutation
     ↓
RETURNING ordinal + generated values
     ↓
ord_map
```

The ordinal is correlation metadata, not a PostgreSQL row-position assumption.
