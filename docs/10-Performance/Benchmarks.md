# Benchmarks

> **Status: benchmark plan, not published performance evidence.**

## Objective

Measure the cost of Foundgine's planning intelligence separately from database execution.

The goal is not to claim that Foundgine replaces EF Core or Dapper. The useful question is:

> **What overhead does semantic resolution and dynamic planning add, and is it small relative to the work performed by the database?**

## Required stages

```text
Metadata construction
JoinGraph construction
Semantic resolution
Read planning
Query planning
Provider compilation
SQL translation
Database execution
End-to-end
```

Record both stage timings and total time.

## Shapes

### Linear

```text
Customer → Account → Transaction
```

### Branching

```text
Customer
 ├── Account → Transaction
 └── ContactPoint
```

### Composite

```text
Customer
 → CustomerBankingRelationship
 → Contract
 → Account
 → Transaction
```

### Repeated entity

```text
Customer
 → Customer
```

or the existing Employee/Manager repeated-entity scenario.

### Collection traversal

```text
Customer
 ├── Account → Transactions
 └── Account → Transactions
```

This should be included once the reusable semantic bridge supports it.

## Measurements

Record:

- median;
- p95;
- allocations;
- generated SQL size where useful;
- database execution time separately from Foundgine planning time;
- warm and cold measurements where meaningful.

## Benchmark rule

Do not optimize based on intuition.

First measure:

```text
resolution
planning
compilation
execution
```

Then optimize only the actual bottleneck.

Do not publish a benchmark conclusion until the benchmark harness, hardware/runtime, dataset and methodology are documented.
