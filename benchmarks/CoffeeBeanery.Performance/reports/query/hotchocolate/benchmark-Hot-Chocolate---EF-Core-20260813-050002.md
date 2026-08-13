# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-13T05:00:02.2404180+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Hot Chocolate + EF Core | 1 | 31.20 | 28.14 ms | 54.09 ms | 57.79 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 8 | 134.40 | 57.49 ms | 81.47 ms | 87.41 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 32 | 137.40 | 234.03 ms | 332.55 ms | 388.38 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 1 | 148.70 | 5.46 ms | 6.55 ms | 88.32 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 8 | 689.20 | 6.88 ms | 26.38 ms | 95.60 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 32 | 1395.20 | 15.81 ms | 93.68 ms | 104.10 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
