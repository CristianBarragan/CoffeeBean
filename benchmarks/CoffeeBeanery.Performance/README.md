# CoffeeBeanery Performance Benchmarks

This benchmark compares a relationship-heavy PostgreSQL graph workload using:

- Hot Chocolate + EF Core
- Foundgine without provider-plan caching
- Foundgine with provider-plan caching

The benchmark runs **one API at a time** against the same database fixture. This keeps the comparison simple and prevents competing API containers from affecting each other.

## Current result

The strongest result is query performance. Across three successful runs, Foundgine delivered roughly **20× the throughput** of the Hot Chocolate + EF Core baseline at concurrency 32, with substantially lower p95 latency.

The provider-plan cache contributes only a small additional improvement, so the main performance difference comes from the underlying Foundgine execution path rather than the cache alone.

Mutation performance is more variable and is treated separately.

## Detailed results

See:

- [Detailed benchmark results](reports/query/BENCHMARK-RESULTS-2026-08-12.md)
- [Benchmark methodology](docs/BENCHMARKS.md)
- [Benchmark isolation](docs/BENCHMARK-ISOLATION.md)

## Run

From:

```powershell
C:\Foundgine\benchmarks\CoffeeBeanery.Performance
```

Run:

```powershell
.\run-query.ps1
```

Or:

```powershell
.\pipelines\query.ps1
```

The query pipeline starts PostgreSQL, initializes the fixture, then sequentially starts and benchmarks Hot Chocolate, Foundgine cold, and Foundgine warm.

Reports are written under:

```text
reports/query/
```
