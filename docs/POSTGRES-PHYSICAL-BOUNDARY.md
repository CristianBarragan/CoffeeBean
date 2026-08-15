# PostgreSQL Physical Boundary

The intended physical compiler contract is:

```text
semantic meaning
    ↓
execution mutation IR
    ↓
derived dependency levels
    ↓
physical PostgreSQL strategy
```

This prevents a provider optimization from becoming a semantic transformation.

A valid optimization may combine independent operations:

```text
A     B     C
└─────┴─────┘
  one physical batch
```

but may not violate:

```text
A → B
```

by placing B before A.

The next audit target is the actual `ord_map` and `RETURNING` implementation.
