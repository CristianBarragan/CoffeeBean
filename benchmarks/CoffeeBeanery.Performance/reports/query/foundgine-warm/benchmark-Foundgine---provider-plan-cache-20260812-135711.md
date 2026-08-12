# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-12T13:57:11.8731696+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - provider-plan cache | 1 | 442.80 | 2.07 ms | 3.16 ms | 3.78 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 8 | 2026.10 | 3.72 ms | 6.01 ms | 7.53 ms | 0 |
| Query top 50 graph | Foundgine - provider-plan cache | 32 | 2882.30 | 10.47 ms | 19.33 ms | 24.30 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 1 | 187.60 | 4.17 ms | 5.04 ms | 81.54 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 8 | 1251.40 | 5.54 ms | 7.20 ms | 20.66 ms | 0 |
| Mutation whole graph | Foundgine - provider-plan cache | 32 | 1154.00 | 14.27 ms | 106.65 ms | 113.51 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
