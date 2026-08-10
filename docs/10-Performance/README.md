# Performance

Performance work starts after correctness.

## Current position

The repository has not yet established benchmark evidence sufficient for claims such as:

- faster than ORM X;
- lower latency than framework Y;
- fewer allocations than Z.

## What should be measured

```text
Metadata construction
JoinGraph construction
Semantic resolution
Read planning
Query planning
Provider compilation
SQL translation
Database execution
Total
```

Test:

```text
1 entity
3 entities
5 entities
10 entities

linear
branching
repeated/self-join
composite
```

## Design priorities

The architecture favors:

- deterministic plans;
- reusable metadata;
- explicit provider plans;
- avoiding runtime reflection where it is unnecessary;
- predictable execution.

Measure before optimizing.
