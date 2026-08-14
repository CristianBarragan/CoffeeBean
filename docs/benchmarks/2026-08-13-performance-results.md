# Foundgine Performance Benchmark — 2026-08-13

## Executive summary

This run extends the CoffeeBeanery benchmark from request throughput/latency into a more useful systems-level measurement by recording **Docker CPU and memory usage** and by exercising mutation request batch sizes of **1, 10, and 50 logical mutations per HTTP request**.

The benchmark uses the same deterministic PostgreSQL fixture and the same GraphQL workload across the tested targets. Measurement phases use a 3 second warm-up and 10 second measurement window at concurrency 1, 8, and 32.

The most important observation is that **batching changes the unit of throughput**. A request containing 50 logical mutations should not be compared with a request containing one mutation using request RPS alone. The benchmark therefore reports both:

- **RPS** — completed HTTP requests per second.
- **Logical/s** — HTTP RPS × mutations represented by each request.

Docker metrics are sampled from the API container during measurement:

- CPU average / maximum
- memory average / maximum / end-of-run

The complete parsed result set is checked in beside this report as CSV and JSON.

## Workloads

### Query: top 50 graph

```text
Customer
  -> CustomerBankingRelationship
      -> Contract
          -> Transaction
```

The query requests the first 50 customers and traverses the complete graph.

### Mutation: whole graph

One logical mutation creates:

```text
Customer
  -> CustomerBankingRelationship
      -> Contract
          -> 2 Transactions
```

Batch sizes 1, 10, and 50 represent multiple independent logical mutations inside one GraphQL request.

### Upsert + select — historical diagnostic only

The historical 2026-08-13 rows labelled `Upsert + select` are retained for traceability, but the implementation used `createCustomer` followed by the top-50 query. It was therefore **not a true upsert workload** and must not be treated as the corrected upsert baseline.

The benchmark harness has since been corrected to perform a real `upsertCustomer` using `CustomerKey` as the conflict identity, followed by the exact same top-50/full-graph query used by `QueryTop50`. The corrected workload must be rerun before publishing new comparative upsert numbers. See [the curated performance analysis](2026-08-13-performance-analysis.md).

## Selected results — concurrency 32

### Query

| Target | RPS | p95 | CPU avg | Memory avg |
|---|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 169.7 | 313.4 ms | 299.3% | 292.2 MB |
| Foundgine — no cache | 3,061.4 | 18.5 ms | 176.3% | 115.0 MB |
| Foundgine — provider-plan cache | 3,047.5 | 18.6 ms | 172.9% | 99.9 MB |

For this run, Foundgine's uncached query path completed roughly **18× the HTTP request throughput** of the Hot Chocolate + EF Core path at concurrency 32, while the p95 latency was roughly **17× lower**. CPU and memory were also substantially lower in the measured API containers.

These are observations from this benchmark environment, not universal performance claims.

### Whole-graph mutation — batch 50

| Target | HTTP RPS | Logical mutations/s | p95 | CPU avg | Memory avg |
|---|---:|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 1,802.5 | 90,125 | 27.6 ms | 183.2% | 164.4 MB |
| Foundgine — no cache | 1,409.8 | 70,490 | 31.1 ms | 79.4% | 79.7 MB |
| Foundgine — provider-plan cache | 1,630.2 | 81,510 | 27.7 ms | 83.5% | 84.7 MB |

The batch-50 result shows why **logical throughput and resource efficiency need to be reported together**. Hot Chocolate produced more logical mutations/s in this particular workload, but Foundgine used considerably less measured API CPU and memory.

### Foundgine upsert + select — batch 50

| Target | HTTP RPS | Logical mutations/s | p95 | CPU avg | Memory avg |
|---|---:|---:|---:|---:|---:|
| Foundgine — no cache | 783.1 | 39,155 | 52.9 ms | 45.8% | 74.0 MB |
| Foundgine — provider-plan cache | 796.7 | 39,835 | 52.5 ms | 46.8% | 72.2 MB |

The provider-plan cache is not a dramatic optimization for this workload. That is useful information: it suggests the next performance work should focus on the larger costs around execution, payloads, database access, and result handling rather than assuming plan caching alone will produce a large end-to-end improvement.

## Batch-size observations

At concurrency 32, Foundgine whole-graph mutation behaved as follows:

| Batch | No cache HTTP RPS | No cache logical/s | Provider-cache HTTP RPS | Provider-cache logical/s |
|---:|---:|---:|---:|---:|
| 1 | 1,600.7 | 1,600.7 | 1,523.8 | 1,523.8 |
| 10 | 1,619.2 | 16,192 | 1,635.4 | 16,354 |
| 50 | 1,409.8 | 70,490 | 1,630.2 | 81,510 |

Batching therefore increases the amount of logical work represented by each HTTP request by an order of magnitude, while the HTTP request rate itself stays in roughly the same range. This is exactly the distinction the benchmark needed to expose.

The batch-50 no-cache result also shows that simply increasing the batch size is not guaranteed to improve latency or logical throughput indefinitely. The larger SQL statement/payload/result set can become another bottleneck. This is an important area for optimization rather than a reason to assume batching is always beneficial.

## What the CPU and memory measurements add

The Docker measurements materially improve the benchmark because throughput alone can hide the cost of achieving that throughput.

The results suggest that Foundgine's current implementation can perform the measured workload with substantially lower API-container CPU and memory than Hot Chocolate + EF Core for the query and mutation workloads tested here.

However, Docker `stats` measures the container as a whole. It does not isolate:

- semantic resolution;
- authorization;
- planning;
- provider compilation;
- SQL execution;
- PostgreSQL CPU/memory;
- network transfer;
- JSON serialization.

The next benchmark layer should therefore add stage-level instrumentation rather than treating container CPU as an explanation of where the time goes.

## Cache strategy — keep the cache work open

The current benchmark should **not** conclude that caching is unimportant. It only shows that the current provider-plan cache has a small end-to-end effect for this particular database-backed workload.

There are several distinct cache opportunities:

```text
Request
  |
  +--> semantic / resolution cache
  |
  +--> authorization / policy cache
  |
  +--> execution-plan cache
  |
  +--> compiled-provider-plan cache
  |
  +--> database/result cache
  |
  +--> serialized response cache
  |
  +--> transport/HTTP response cache
  |
  +--> Database / PostgreSQL caches
```

In particular, **result caching after the database response** can be valuable when many requests repeat the same semantic query. A response/result cache can sit above the provider and avoid database execution altogether for eligible read workloads.

That is intentionally a future benchmark dimension rather than something to fold into the current numbers.

## Next benchmark: payload sensitivity

The next controlled experiment should vary payload size independently from concurrency:

| Payload | Example |
|---|---|
| Small | id + a few scalar fields |
| Medium | normal application projection |
| Large | full relationship graph / large response |

Each payload should be tested with:

- no cache;
- provider-plan cache;
- database/result cache;
- response cache;
- cold and warm cache states.

This will answer the important question: **when does caching become worthwhile because execution and serialization are no longer the dominant costs?**

## Future comparison: FASTER

A future benchmark should also add **FASTER** as another cache/storage option. The comparison should measure it as a concrete infrastructure component, not as an abstract claim about caching.

The useful questions are:

1. What is the hit/miss overhead?
2. What throughput does it sustain?
3. How much memory does it consume?
4. How does it behave with small versus very large payloads?
5. Does it reduce PostgreSQL load enough to justify its memory and serialization costs?

## Important benchmark limitation

This is a benchmark result captured from the current development environment, not a formal performance certification. Hardware, Docker configuration, PostgreSQL configuration, network conditions, runtime version, and workload shape all influence the result.

The repository should use this report as a **reproducible engineering baseline**, not as a marketing guarantee.

## Files

- `reports/benchmarks/2026-08-13/results.csv` — complete parsed measurement rows.
- `reports/benchmarks/2026-08-13/results.json` — same data in machine-readable form.
- `benchmarks/CoffeeBeanery.Performance/` — benchmark runner and Docker orchestration.
