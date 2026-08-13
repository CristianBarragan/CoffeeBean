# P2 — Benchmark Baseline

## Goal

Measure the cost of the semantic execution layer without conflating different kinds of overhead.

The benchmark suite is divided into two levels:

1. **HTTP workload benchmark** — existing CoffeeBeanery/PostgreSQL workload measuring end-to-end request latency and throughput.
2. **Pipeline benchmark** — future microbenchmark for resolution, authorization, planning, compilation, and cached-plan reuse without network/database noise.

This priority freezes the methodology and baseline. It does not invent performance claims that the repository does not currently measure.

## Current demonstrated benchmark

The existing workload compares:

| Target | Query | Mutation | Cache |
|---|---|---|---|
| Hot Chocolate + EF Core | yes | yes | runtime/EF only |
| Foundgine cold | yes | yes | no provider-plan cache |
| Foundgine warm | yes | yes | provider-plan cache for reads |

The workload is identical across targets:

- 1,000 customers
- Customer → CustomerBankingRelationship → Contract → Transaction traversal
- top 50 query
- whole-graph mutation
- concurrency 1, 8, 32
- 3 second warm-up
- 10 second measurement

Correctness preflight runs before performance measurement and HTTP errors / GraphQL error responses are treated as failed benchmark rows.

## Existing baseline

The checked-in reports from 2026-08-12 provide the current baseline.

### Query — concurrency 32

| Target | RPS | p50 | p95 | p99 | Errors |
|---|---:|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 137.60 | 232.18 ms | 349.83 ms | 393.28 ms | 0 |
| Foundgine cold | 2,949.60 | 10.23 ms | 18.85 ms | 23.81 ms | 0 |
| Foundgine warm | 2,909.10 | 10.34 ms | 19.43 ms | 24.72 ms | 0 |

### Query — concurrency 1

| Target | RPS | p50 | p95 | p99 | Errors |
|---|---:|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 33.30 | 27.00 ms | 51.52 ms | 58.14 ms | 0 |
| Foundgine cold | 461.70 | 1.97 ms | 3.07 ms | 3.89 ms | 0 |
| Foundgine warm | 467.70 | 1.96 ms | 2.98 ms | 3.63 ms | 0 |

These are **existing repository measurements**, not a new controlled run performed for this priority. They should therefore be treated as a baseline for reproducibility, not as a universal performance claim.

## What the current benchmark proves

It demonstrates that the repository has a repeatable end-to-end workload comparing Foundgine cold/warm execution with Hot Chocolate + EF Core on the same PostgreSQL fixture.

It also gives an initial indication that provider-plan caching has a relatively small effect on this particular end-to-end workload because database/network execution dominates the request.

It does **not** isolate the cost of:

- semantic resolution;
- authorization;
- logical planning;
- provider compilation;
- plan-cache lookup;
- materialization.

## Required microbenchmark matrix

The next benchmark layer should isolate these stages:

```text
Request
  ├── Resolve
  ├── Authorize
  ├── Plan
  ├── Compile
  ├── Cache lookup
  └── Execute
```

The minimum matrix is:

| Scenario | Purpose |
|---|---|
| Direct provider execution | physical baseline |
| Foundgine uncached | full semantic + compile overhead |
| Foundgine cached | steady-state semantic overhead |
| Resolution only | intent → semantic graph |
| Authorization only | authorization cost |
| Planning only | semantic graph → execution plan |
| Compilation only | execution plan → provider plan |

AOT is **not included in this baseline** because the current repository does not contain a controlled AOT benchmark proving an equivalent workload. It should be added only when an apples-to-apples AOT scenario exists.

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
