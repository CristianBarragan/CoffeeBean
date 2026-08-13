# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-13T06:43:58.1501900+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - no cache | 1 | 453.70 | 2.03 ms | 3.02 ms | 3.68 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 8 | 2383.40 | 3.15 ms | 5.04 ms | 6.55 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 32 | 3285.90 | 9.16 ms | 16.75 ms | 21.77 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 1 | 173.30 | 4.12 ms | 5.14 ms | 90.91 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 8 | 848.90 | 5.34 ms | 8.02 ms | 95.14 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 32 | 1178.20 | 16.47 ms | 95.97 ms | 102.49 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
