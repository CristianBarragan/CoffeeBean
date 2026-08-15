# SQL correlation contract — SQL Correlation Contract

This stage is intentionally conservative.

The physical contract is now explicit:

```text
logical input ordinal
        ↓
physical mutation
        ↓
RETURNING ordinal + generated values
        ↓
ord_map
        ↓
dependent operation
```

The concrete compiler must satisfy this contract without relying on returned
row position.

The uploaded source was inspected before making changes. No speculative SQL
rewrite was made where the concrete SQL-builder API could not be established.

Next stage: wire the contract into the actual PostgreSQL SQL generation
after inspecting its concrete builder types.
