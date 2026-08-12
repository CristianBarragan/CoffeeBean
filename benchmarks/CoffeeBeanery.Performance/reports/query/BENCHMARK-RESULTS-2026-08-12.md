# CoffeeBeanery Performance Results

**Benchmark date:** 12 August 2026  
**Runs:** 3 successful runs  
**Workload:** deterministic PostgreSQL graph workload  
**Measurement:** 10 seconds per case  
**Warm-up:** 3 seconds  
**Concurrency:** 1, 8, 32  
**Request timeout:** 5 seconds

## Executive summary

The query benchmark is consistently and substantially faster with Foundgine than with the Hot Chocolate + EF Core baseline.

Across three independent successful runs, the average query throughput at concurrency 32 was:

| Implementation | Average RPS | Average p95 |
|---|---:|---:|
| Hot Chocolate + EF Core | **139.4** | **338.4 ms** |
| Foundgine — no cache | **2,781.0** | **20.3 ms** |
| Foundgine — provider-plan cache | **2,838.9** | **19.9 ms** |

At concurrency 32, that is approximately:

- **20.0× the throughput** for Foundgine without the cache.
- **20.4× the throughput** for Foundgine with the provider-plan cache.
- About **16.7× lower p95 latency** for Foundgine without the cache.
- About **17.0× lower p95 latency** for Foundgine with the cache.

The large query advantage is therefore **not dependent on the provider-plan cache**.

Mutation performance is less uniform. Foundgine is competitive and can be faster at higher concurrency, but the results vary more between runs. Mutation performance should not currently be presented as the primary benchmark claim.

## Benchmark setup

All three API implementations were tested against the same benchmark database during each run:

```text
Customer
  -> CustomerBankingRelationship
      -> Contract
          -> Transaction
```

Deterministic fixture:

- 1,000 customers
- 4,000 relationships
- 12,000 contracts
- 48,000 transactions

Customer 1 is the deterministic benchmark target.

The benchmark intentionally runs **one API at a time**:

1. PostgreSQL starts.
2. The database fixture is initialized.
3. Hot Chocolate starts and is benchmarked.
4. Hot Chocolate is stopped.
5. Foundgine cold starts and is benchmarked.
6. Foundgine cold is stopped.
7. Foundgine warm starts and is benchmarked.
8. Foundgine warm is stopped.
9. PostgreSQL is stopped.

This avoids running the competing API containers simultaneously.

## Query results — all three runs

### Throughput (RPS)

| Concurrency | Hot Chocolate Run 1 | Foundgine Cold Run 1 | Foundgine Warm Run 1 | Hot Chocolate Run 2 | Foundgine Cold Run 2 | Foundgine Warm Run 2 | Hot Chocolate Run 3 | Foundgine Cold Run 3 | Foundgine Warm Run 3 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 32.4 | 428.0 | 437.2 | 33.1 | 423.3 | 442.8 | 33.3 | 461.7 | 467.7 |
| 8 | 135.7 | 1,988.4 | 2,017.6 | 134.3 | 1,927.4 | 2,026.1 | 141.0 | 2,133.7 | 2,207.7 |
| 32 | 140.9 | 2,752.0 | 2,725.4 | 139.6 | 2,641.5 | 2,882.3 | 137.6 | 2,949.6 | 2,909.1 |

### p95 latency

| Concurrency | Hot Chocolate Run 1 | Foundgine Cold Run 1 | Foundgine Warm Run 1 | Hot Chocolate Run 2 | Foundgine Cold Run 2 | Foundgine Warm Run 2 | Hot Chocolate Run 3 | Foundgine Cold Run 3 | Foundgine Warm Run 3 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 50.9 ms | 3.3 ms | 3.3 ms | 52.3 ms | 3.3 ms | 3.2 ms | 51.5 ms | 3.1 ms | 3.0 ms |
| 8 | 78.5 ms | 6.3 ms | 6.0 ms | 79.8 ms | 6.4 ms | 6.0 ms | 77.5 ms | 5.7 ms | 5.5 ms |
| 32 | 326.9 ms | 20.4 ms | 20.9 ms | 338.6 ms | 21.7 ms | 19.3 ms | 349.8 ms | 18.8 ms | 19.4 ms |

## Average query results

| Concurrency | Hot Chocolate RPS | Foundgine Cold RPS | Foundgine Warm RPS | Cold advantage | Warm advantage |
|---:|---:|---:|---:|---:|---:|
| 1 | 32.9 | 437.7 | 449.2 | **13.3×** | **13.6×** |
| 8 | 137.0 | 2,016.5 | 2,083.8 | **14.7×** | **15.2×** |
| 32 | 139.4 | 2,781.0 | 2,838.9 | **20.0×** | **20.4×** |

Average p95 latency:

| Concurrency | Hot Chocolate | Foundgine Cold | Foundgine Warm |
|---:|---:|---:|---:|
| 1 | 51.6 ms | 3.2 ms | 3.2 ms |
| 8 | 78.6 ms | 6.1 ms | 5.8 ms |
| 32 | 338.4 ms | 20.3 ms | 19.9 ms |

## Cache effect

The provider-plan cache has only a modest effect on query throughput:

| Concurrency | Cold average RPS | Warm average RPS | Difference |
|---:|---:|---:|---:|
| 1 | 437.7 | 449.2 | +2.6% |
| 8 | 2,016.5 | 2,083.8 | +3.3% |
| 32 | 2,781.0 | 2,838.9 | +2.1% |

This is an important result.

The large Foundgine advantage is present **without** the provider-plan cache. The cache is an optimization on top of an already faster execution path rather than the explanation for the overall performance difference.

## Mutation results

### Throughput (RPS)

| Concurrency | Hot Chocolate Run 1 | Foundgine Cold Run 1 | Foundgine Warm Run 1 | Hot Chocolate Run 2 | Foundgine Cold Run 2 | Foundgine Warm Run 2 | Hot Chocolate Run 3 | Foundgine Cold Run 3 | Foundgine Warm Run 3 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 182.6 | 231.6 | 231.5 | 179.0 | 230.7 | 187.6 | 156.4 | 209.8 | 258.1 |
| 8 | 1,078.4 | 735.9 | 1,010.9 | 1,077.2 | 727.5 | 1,251.4 | 1,039.4 | 740.8 | 741.2 |
| 32 | 1,173.1 | 1,379.3 | 831.9 | 914.8 | 1,247.8 | 1,154.0 | 992.3 | 1,559.6 | 1,521.9 |

Mutation results are more variable than query results. The strongest repeatable conclusion is that Foundgine can perform well at high concurrency, but the provider-plan cache is not a consistent mutation optimization and Hot Chocolate + EF Core remains competitive at some concurrency levels.

## Reliability

The three successful benchmark runs reported:

- **0 application errors**
- **0 request timeouts**
- **0 cancelled requests**

The benchmark fixture was initialized successfully at the start of the successful runs.

## What the benchmark demonstrates

The strongest evidence is for **read/query execution over a relationship-heavy graph**.

The results consistently show:

1. Foundgine has substantially higher query throughput.
2. Foundgine maintains much lower p95 latency as concurrency increases.
3. The advantage remains when provider-plan caching is disabled.
4. Provider-plan caching provides a small additional query improvement.
5. Mutation performance is promising but not yet as conclusive.

## Scope and limitations

This is a controlled CoffeeBeanery workload, not a universal framework benchmark.

Results depend on the:

- schema and graph shape;
- PostgreSQL version and configuration;
- benchmark host;
- deterministic fixture;
- exact query and mutation operations;
- current Foundgine implementation;
- current Hot Chocolate + EF Core implementation.

The appropriate conclusion is therefore:

> **Foundgine demonstrates a substantial performance advantage for this relationship-heavy graph query workload.**

It should not be interpreted as a claim that Foundgine is universally faster than EF Core or Hot Chocolate for every workload.
