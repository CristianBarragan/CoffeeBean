# Benchmarks

> **Status: benchmark plan, not published performance evidence.**

The next benchmark should measure the active Foundgine pipeline rather than attempt to replace or defeat EF Core/Dapper on raw database access.

```text
Metadata
 → Semantic resolution
 → Read planning
 → Query planning
 → Provider compilation
 → SQL translation
 → Database execution
```

Measure each stage separately and report total end-to-end cost.

## Required shapes

```text
Customer → Account → Transaction

Customer
 ├── Account → Transaction
 └── ContactPoint

Customer
 → CustomerBankingRelationship
 → Contract
 → Account
 → Transaction

Customer → Customer (repeated occurrence)
```

Add a multi-account collection traversal case once the reusable semantic bridge supports it.

The useful question is not "is Foundgine faster than Dapper?" It is:

> **What does dynamic semantic/planning execution cost, and is that overhead reasonable compared with the actual database work it enables?**

See [Performance Benchmarks](docs/10-Performance/Benchmarks.md).
