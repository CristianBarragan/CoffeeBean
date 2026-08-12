# CoffeeBeanery / Three API Performance Benchmark

- Generated: `2026-08-12T14:03:55.2045576+00:00`
- Warm-up: `3s`
- Measurement: `10s`
- Request timeout: `5s`
- Readiness timeout: `120s`
- Drain timeout: `30s`
- Concurrency: `1, 8, 32`

## Results

| Operation | Target | Concurrency | RPS | p50 | p95 | p99 | Errors |
|---|---|---:|---:|---:|---:|---:|---:|
| Query top 50 graph | Foundgine - no cache | 1 | 461.70 | 1.97 ms | 3.07 ms | 3.89 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 8 | 2133.70 | 3.55 ms | 5.70 ms | 7.18 ms | 0 |
| Query top 50 graph | Foundgine - no cache | 32 | 2949.60 | 10.23 ms | 18.85 ms | 23.81 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 1 | 209.80 | 3.87 ms | 4.89 ms | 8.12 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 8 | 740.80 | 5.39 ms | 91.50 ms | 100.78 ms | 0 |
| Mutation whole graph | Foundgine - no cache | 32 | 1559.60 | 15.21 ms | 93.56 ms | 104.01 ms | 0 |

## Workloads

**MutationWholeGraph** creates one Customer, one CustomerBankingRelationship, one Contract and two Transactions in one GraphQL mutation. The child foreign keys are resolved from the parent operation results by Foundgine.

**QueryTop50** selects the first 50 customers and traverses Customer -> CustomerBankingRelationship -> Contract -> Transaction.

The Foundgine warm cache applies to the provider execution plan for the read/query workload. Mutation plans are compiled per request because their parameter values are intentionally dynamic.

Each warm-up and measurement phase has a hard wall-clock boundary. At phase expiry, new requests stop immediately and in-flight HTTP requests are cancelled. Cancelled requests at the phase boundary are not counted as errors or measured samples.

Requests that exceed the explicit request timeout are counted as timeouts. The benchmark no longer uses a 30-second HttpClient timeout to drain a phase.
