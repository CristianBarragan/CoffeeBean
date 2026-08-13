# P2 — Benchmark Baseline

## Goal

Measure the cost of the semantic execution layer without conflating different kinds of overhead.

The benchmark suite is divided into two levels:

1. **HTTP workload benchmark** — existing CoffeeBeanery/PostgreSQL workload measuring end-to-end request latency and throughput.
2. **Pipeline benchmark** — future microbenchmark for resolution, authorization, planning, compilation, and cached-plan reuse without network/database noise.

This priority freezes the methodology and baseline. It does not invent performance claims that the repository does not currently measure.

## Current demonstrated benchmark

The current CoffeeBeanery benchmark now measures:

- Hot Chocolate + EF Core;
- Foundgine without provider-plan cache;
- Foundgine with provider-plan cache;
- query and whole-graph mutation workloads;
- mutation batch sizes 1, 10, and 50;
- concurrency 1, 8, and 32;
- p50/p95/p99 latency;
- HTTP requests/s and logical operations/s;
- Docker API-container CPU and memory.

The deterministic fixture remains:

- 1,000 customers;
- 4,000 relationships;
- 12,000 contracts;
- 48,000 transactions.

The 2026-08-13 result is checked in at [`docs/benchmarks/2026-08-13-performance-results.md`](benchmarks/2026-08-13-performance-results.md). The raw parsed rows are in `reports/benchmarks/2026-08-13/`.

### Important limitation

Hot Chocolate + EF Core currently has a correctness bug in the GraphQL **upsert + select** workload. That workload is therefore excluded from comparative conclusions until the implementation is fixed. Foundgine's upsert numbers are retained as an internal performance measurement.

## What the current benchmark shows

The run shows that batching materially increases logical work represented by each HTTP request, while request RPS remains in a similar range. It also shows that Foundgine uses substantially less measured API-container CPU and memory in the tested query/mutation scenarios, while the exact throughput advantage depends strongly on workload and batch size.

The provider-plan cache has a relatively small end-to-end effect for this database-backed workload. That is not evidence that caching is unimportant; it means the current benchmark is dominated by costs outside provider-plan lookup for these scenarios.

There is significant room for optimization. Future work should investigate multiple cache levels, including:

1. semantic/resolution cache;
2. authorization/policy cache;
3. execution/provider-plan cache;
4. database/result cache after the DB response;
5. serialized response cache;
6. HTTP/transport cache where appropriate.

## Next benchmark matrix

The next controlled benchmark should vary payload size as well as concurrency and batch size:

| Dimension | Values |
|---|---|
| Payload | small, medium, large |
| Mutation batch | 1, 10, 50 |
| Concurrency | 1, 8, 32 |
| Plan cache | off, on |
| Result cache | off, on |
| Response cache | off, on |
| Cache state | cold, warm |

The large-vs-small payload test is particularly important because response/result caching may become much more valuable once serialization, network transfer, or repeated database work dominates.

FASTER should be added as a future cache/storage comparison once the payload matrix exists.

## Rules

1. Never compare different workloads.
2. Never compare different database fixtures.
3. Never mix cold-start and steady-state numbers.
4. Report errors separately from latency.
5. Report p50/p95/p99, not only averages.
6. Keep database/network benchmarks separate from in-process pipeline benchmarks.
7. Do not claim Foundgine is faster than another framework from a single benchmark run.
8. Publish the workload, configuration, commit, and environment with benchmark results.

## Reproduction

From `benchmarks/CoffeeBeanery.Performance`:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\run-query.ps1
```

The resulting reports are written under `reports/query/`.
