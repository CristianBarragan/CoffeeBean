# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-12T14:02:32.1049807+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Hot Chocolate + EF Core | 1 | 33.30 | 27.00 ms | 51.52 ms | 58.14 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 8 | 141.00 | 53.66 ms | 77.48 ms | 89.99 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 32 | 137.60 | 232.18 ms | 349.83 ms | 393.28 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 1 | 156.40 | 5.21 ms | 6.90 ms | 86.92 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 8 | 1039.40 | 7.06 ms | 9.61 ms | 12.67 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 32 | 992.30 | 16.16 ms | 100.94 ms | 274.66 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
