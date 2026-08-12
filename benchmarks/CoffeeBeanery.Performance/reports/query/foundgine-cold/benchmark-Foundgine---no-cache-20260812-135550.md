# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-12T13:55:50.6396707+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - no cache | 1 | 423.30 | 2.18 ms | 3.34 ms | 4.09 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 8 | 1927.40 | 3.91 ms | 6.42 ms | 8.29 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 32 | 2641.50 | 11.33 ms | 21.67 ms | 27.75 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 1 | 230.70 | 4.21 ms | 5.22 ms | 5.73 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 8 | 727.50 | 5.52 ms | 89.52 ms | 101.31 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 32 | 1247.80 | 16.53 ms | 104.33 ms | 111.01 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
