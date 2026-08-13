# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-13T06:42:17.5556688+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Hot Chocolate + EF Core | 1 | 33.10 | 27.12 ms | 51.72 ms | 59.46 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 8 | 143.30 | 53.94 ms | 74.65 ms | 80.36 ms | 0 |
| Query top 50 graph | Hot Chocolate + EF Core | 32 | 145.90 | 219.65 ms | 317.66 ms | 360.94 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 1 | 182.60 | 5.36 ms | 6.16 ms | 6.90 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 8 | 1121.50 | 6.90 ms | 9.00 ms | 11.33 ms | 0 |
| Mutation whole graph | Hot Chocolate + EF Core | 32 | 1222.20 | 15.50 ms | 96.21 ms | 104.12 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
