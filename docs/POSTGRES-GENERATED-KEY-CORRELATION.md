# PostgreSQL Generated-Key Correlation Invariants

This stage freezes the correctness contract for generated-key correlation.

For a dependency:

```text
source operation
      ↓
generated return field
      ↓
ordinal correlation
      ↓
dependent operation
```

the physical lowering must preserve the logical identity of every source operation.

## Required invariants

### 1. Source operation must exist

A reference cannot resolve against an operation that is absent from the mutation graph.

### 2. Source return field must exist

A dependent operation may only consume a value that the source operation returns.

### 3. Ordinals are scoped to the source operation/group

An ordinal is not a global identity. It identifies the logical row within the source operation's result mapping.

### 4. Dependency ordering is mandatory

A dependent operation cannot execute before its source value is materialized.

### 5. Multiple consumers are allowed

One generated key may be consumed by multiple downstream operations without duplicating the source mutation.

```text
Create Customer
      │
      ├── Create Account
      └── Create Address
```

### 6. Cross-group references require an explicit correlation mapping

A provider batch compiler must never assume that two physical groups share row ordinals unless that relationship was established by the execution IR.

### 7. Missing or ambiguous correlation is a compile-time/provider-capability failure

The provider must reject or fall back rather than emit SQL whose returned rows cannot be mapped back to logical operations.

## Why this matters

Generated database keys are physical values, but the relationship between:

```text
logical operation → generated value → dependent operation
```

is semantic execution state.

The compiler must preserve that mapping through batching.

A faster batch with incorrect correlation is not a valid optimization.

## Generated-key correlation carrier carrier

For PostgreSQL 17+ batched Create groups, the compiler uses `MERGE ... RETURNING` so the source-side compiler-owned `__fg_corr` is returned directly alongside generated target values. This removes the previous need to re-identify inserted rows by a user-visible conflict/natural key. Upsert remains on its existing `ON CONFLICT` path because `MERGE` and `ON CONFLICT` have materially different concurrency semantics.
