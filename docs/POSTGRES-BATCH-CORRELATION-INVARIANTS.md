# PostgreSQL Batch Correlation Invariants

The PostgreSQL mutation compiler may batch operations only when logical
operation identity can be preserved.

## Duplicate conflict identity

For `Create` operations, duplicate literal correlation keys are rejected from
the batch and use the sequential fallback.

The same rule now applies to `Upsert` when all conflict columns are literal.

Why:

```text
operation A ─┐
             ├── same conflict key ──> one PostgreSQL target row
operation B ─┘
```

A PostgreSQL `ON CONFLICT` operation can legitimately collapse these physical
writes, but the semantic mutation graph still contains two logical operations.
Returning one row cannot safely establish two operation results or generated
value mappings.

Therefore:

```text
duplicate literal conflict key
        ↓
batch compiler rejects
        ↓
TryCompile returns null
        ↓
sequential provider executes
```

This is a correctness boundary, not merely an optimization decision.

## Source-valued conflict keys

Conflict identities that depend on previous operation results are not
statically deduplicated here. They are resolved through the dependency graph
and must remain one-to-one through the generated ord-map. Any ambiguity that
cannot be represented safely must cause compilation to fall back rather than
silently corrupt correlation.
