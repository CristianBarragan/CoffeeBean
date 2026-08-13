# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-13T06:45:37.4323648+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - provider-plan cache | 1 | 468.00 | 1.98 ms | 2.98 ms | 3.59 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 8 | 2452.80 | 3.07 ms | 4.88 ms | 6.15 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 32 | 3363.20 | 8.93 ms | 16.52 ms | 21.22 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 1 | 183.40 | 4.12 ms | 5.24 ms | 81.87 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 8 | 765.90 | 5.45 ms | 81.17 ms | 90.27 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 32 | 1571.40 | 14.12 ms | 95.88 ms | 105.08 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
