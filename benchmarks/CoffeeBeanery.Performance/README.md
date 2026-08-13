# CoffeeBeanery Performance Benchmark

The benchmark measures Foundgine and Hot Chocolate + EF Core against the same deterministic PostgreSQL workload.

## Current benchmark matrix

- PostgreSQL fixture: 1,000 customers, 4,000 relationships, 12,000 contracts, 48,000 transactions.
- Concurrency: 1, 8, 32.
- Query: top-50 relationship graph.
- Whole-graph mutation: batch sizes 1, 10, 50.
- Docker metrics: API-container CPU and memory sampled during measurement.
- Latency: p50, p95, p99.
- Throughput: HTTP requests/s and logical operations/s for batched mutations.
- Warm-up: 3 seconds.
- Measurement: 10 seconds.

Batch size is only applied to the mutation workload. Query results remain batch size 1 so the query comparison stays semantically identical.

## Run

From `benchmarks/CoffeeBeanery.Performance`:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\run-query.ps1
```

The runner sets:

```text
BENCHMARK_CONCURRENCY=1,8,32
BENCHMARK_BATCH_SIZES=1,10,50
BENCHMARK_DOCKER_CONTAINER=<API container>
```

Docker metrics are collected with `docker stats --no-stream` while the measurement is running. CPU is reported as a percentage of one logical CPU, so values above 100% mean multiple CPUs are being used by the container.

## Interpreting batch results

Do not compare only HTTP RPS when batch sizes differ.

For example:

```text
batch=1   -> 1 HTTP request contains 1 logical mutation
batch=50  -> 1 HTTP request contains 50 logical mutations
```

The benchmark therefore reports:

```text
HTTP RPS
logical/s = HTTP RPS × batch size
```

Latency remains the latency of the HTTP request containing the entire batch.

## Known limitation

Hot Chocolate + EF Core currently has a correctness bug in the **GraphQL upsert + select** workload, so that workload is not considered a valid comparative baseline. Foundgine's upsert workload remains useful as an internal measurement and should be compared against Hot Chocolate only after the external implementation is fixed.

## Results

The checked-in 2026-08-13 run is documented in [`docs/benchmarks/2026-08-13-performance-results.md`](../../docs/benchmarks/2026-08-13-performance-results.md), with machine-readable data under `reports/benchmarks/2026-08-13/`.
