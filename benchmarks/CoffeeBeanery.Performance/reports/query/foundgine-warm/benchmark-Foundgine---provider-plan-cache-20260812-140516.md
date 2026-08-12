# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-12T14:05:16.7598415+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - provider-plan cache | 1 | 467.70 | 1.96 ms | 2.98 ms | 3.63 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 8 | 2207.70 | 3.43 ms | 5.50 ms | 6.94 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 32 | 2909.10 | 10.34 ms | 19.43 ms | 24.72 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 1 | 258.10 | 3.76 ms | 4.68 ms | 5.50 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 8 | 741.20 | 5.49 ms | 90.56 ms | 98.39 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 32 | 1521.90 | 13.85 ms | 101.01 ms | 113.99 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
