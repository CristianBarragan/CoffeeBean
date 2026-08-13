# Benchmark Methodology

The benchmark compares the same PostgreSQL-backed graph workload across:

| Service | Read path | Write path | Cache |
|---|---|---|---|
| Hot Chocolate + EF Core | EF Core `Include` graph | EF Core `SaveChangesAsync` graph insert | EF Core/ASP.NET runtime only |
| Foundgine cold | Semantic model -> authorization -> planner -> SQL compiler -> execution | GraphQL mutation adapter -> nested mutation planner -> SQL mutation compiler -> dependency-aware execution | No provider-plan cache |
| Foundgine warm | Same Foundgine pipeline | Same Foundgine mutation pipeline | Provider execution plan cached for reads |

## Correctness comes first

The loader performs a readiness check and then a correctness preflight for every API. It executes one representative query and one representative mutation and rejects HTTP errors and GraphQL responses containing an `errors` array.

A broken API or workload never stops the remaining matrix. Failed rows are marked diagnostic and are excluded from performance comparisons.

## Measurement

The loader measures completed GraphQL requests/second and p50/p95/p99 latency at each configured concurrency level. Request timeouts are tracked separately from application errors. A request already in flight when the measurement window expires is allowed to finish; the per-request timeout prevents a broken target from hanging a benchmark run indefinitely.

For every measurement phase, the loader can also sample the target Docker container with `docker stats --no-stream`. The report records average/max CPU percentage and average/max/end memory in MB. Docker metrics are diagnostic and never make a benchmark fail.

The upsert workload is exercised with logical batch sizes `1`, `10`, and `50`. These are burst sizes in the benchmark driver: each worker issues that many normal GraphQL HTTP operations back-to-back before starting the next batch. They are **not** an HTTP GraphQL batch request and should not be presented as server-side SQL batch sizes.

## Workloads

### Query — top 50

The loader selects the first 50 customers and traverses:

`Customer -> CustomerBankingRelationship -> Contract -> Transaction`

The fixture contains 1,000 customers, 4 relationships per customer, 3 contracts per relationship, and 4 transactions per contract by default.

### Mutation — whole graph create


The loader creates:

`Customer -> CustomerBankingRelationship -> Contract -> 2 Transactions`

Mutation keys are generated dynamically for every request, so the benchmark does not repeatedly upsert or collide with one fixed identity.

## Cache comparison

The Foundgine warm configuration caches only the provider execution plan for the read workload. Mutation plans remain request-specific because the values are intentionally dynamic.
