# CoffeeBeanery Performance Analysis — 2026-08-13 Baseline

## Executive summary

This is the curated engineering analysis of the supplied 2026-08-13 CoffeeBeanery benchmark run.

The run is correctness-gated: the reported measurement rows completed with `errors=0` and `timeouts=0`.
It compares:

- Hot Chocolate + EF Core
- Foundgine — no cache
- Foundgine — provider-plan cache

The fixture contains 1,000 customers, 4,000 relationships, 12,000 contracts and 48,000 transactions.
The graph under test is:

```text
Customer
  └── 4 CustomerBankingRelationships
        └── 3 Contracts each
              └── 4 Transactions each
```

The most important result is that **Foundgine's current query path is substantially faster than Hot Chocolate + EF Core on this particular graph workload**, while using substantially less measured API-container memory and CPU at high concurrency.

Mutation performance is closer: Hot Chocolate + EF Core leads at concurrency 32, but Foundgine uses materially fewer measured API-container resources.

The upsert + select workload is the largest current optimization target. Importantly, the benchmark harness has now been corrected so that this workload is a **real upsert followed by the exact same top-50/full-graph query used by the standalone query benchmark**. The new implementation must therefore be rerun before publishing new comparative upsert numbers; older create-then-select rows must not be used as evidence for the corrected workload.

These are workload-specific engineering observations, not universal performance claims.

---

## 1. Methodology

### Environment

- .NET 9 containers
- PostgreSQL
- 1,000 customers
- 4,000 customer-banking relationships
- 12,000 contracts
- 48,000 transactions
- deterministic benchmark target and fixture
- 3 second warm-up
- 10 second measurement
- 5 second request timeout
- concurrency 1, 8, 32 in the curated baseline
- mutation batch sizes 1, 10, 50
- Docker CPU and memory sampled during measurement

### Workloads

#### Query — top 50 graph

The query is identical across targets:

```graphql
customer(first: 50) {
  id
  customerKey
  firstName
  lastName
  fullName
  customerBankingRelationship {
    id
    customerBankingRelationshipKey
    contract {
      id
      contractKey
      amount
      transaction {
        id
        transactionKey
        amount
        balance
      }
    }
  }
}
```

This is a full relationship graph, not a scalar-only query.

#### Whole-graph mutation

A logical mutation creates:

```text
Customer
  └── CustomerBankingRelationship
        └── Contract
              ├── Transaction
              └── Transaction
```

Batch size means multiple logical mutations represented by one HTTP request. Therefore both HTTP RPS and logical/s are reported.

#### Corrected upsert + select

The benchmark now measures one logical client operation:

```text
REAL UPSERT
    ↓
exact same top-50 query
    ↓
full relationship graph
```

The upsert targets existing deterministic customers using `CustomerKey` as the conflict identity. Batch sizes 1, 10 and 50 represent independent existing-row upserts. The following select is always the same `customer(first: 50)` full graph used by the standalone query workload.

One stopwatch spans the complete write-then-refetch operation, so latency represents what the client actually pays for the combined workflow.

This is deliberately different from the earlier benchmark implementation, which used `createCustomer` and then queried. Those older rows are not a valid upsert comparison and should not be mixed with the corrected baseline.

---

# 2. Findings

## Finding 1 — The query path is currently the strongest Foundgine result

At concurrency 32 the supplied baseline measured:

| Implementation | RPS | p50 | p95 | p99 | CPU avg | Memory avg |
|---|---:|---:|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 156.7 | 183.5 ms | 348.6 ms | 417.6 ms | 301.5% | 292.7 MB |
| Foundgine — no cache | 2,781.6 | 10.7 ms | 20.5 ms | 27.4 ms | 176.2% | 97.0 MB |
| Foundgine — provider-plan cache | 3,012.6 | 10.0 ms | 18.6 ms | 24.0 ms | 177.8% | 79.7 MB |

For this graph workload, Foundgine's provider-plan cache path delivers about **19.2× the measured HTTP request throughput** of Hot Chocolate + EF Core at concurrency 32, with a much lower p99 latency.

The resource profile is also important. Foundgine is not achieving the throughput by simply consuming more application-container CPU or memory. At C=32 it uses substantially less of both.

That does **not** prove lower total system cost. PostgreSQL CPU/memory, host contention, network transfer and other infrastructure costs are outside this measurement.

### Query at lower concurrency

At concurrency 1:

- Hot Chocolate + EF Core: 30.4 RPS
- Foundgine no cache: 408.2 RPS
- Foundgine provider-plan cache: 466.7 RPS

At concurrency 8:

- Hot Chocolate + EF Core: 105.7 RPS
- Foundgine no cache: 2,306.1 RPS
- Foundgine provider-plan cache: 2,329.3 RPS

The result is therefore not isolated to a single high-concurrency point.

---

## Finding 2 — Provider-plan caching helps, but it is not the whole performance story

For the top-50 query at C=32:

```text
Foundgine no cache       2,781.6 RPS
Foundgine warm cache     3,012.6 RPS
Improvement                  ~8.3%
```

For the corrected upsert + select workload, the cache should be interpreted separately after the new run because the workload semantics have changed.

The architectural implication is important:

> Provider-plan caching removes repeated compilation work. It does not cache the database result.

The current cache therefore sits here:

```text
Semantic resolution
      ↓
Authorization
      ↓
Provider-plan cache
      ↓
PostgreSQL
      ↓
Result shaping
      ↓
Transport
```

A plan cache cannot eliminate PostgreSQL execution, row materialization, graph shaping or serialization.

---

## Finding 3 — Mutation is competitive, but not currently ahead

At concurrency 32 and batch 50:

| Implementation | HTTP RPS | Logical/s | p50 | p95 | p99 |
|---|---:|---:|---:|---:|---:|
| Hot Chocolate + EF Core | 1,739.1 | 86,955 | 17.5 ms | 27.6 ms | 35.5 ms |
| Foundgine — no cache | 1,393.5 | 69,675 | 19.9 ms | 45.4 ms | 54.3 ms |
| Foundgine — provider-plan cache | 1,638.2 | 81,910 | 19.1 ms | 27.6 ms | 33.5 ms |

Hot Chocolate + EF Core is ahead in logical throughput, but the resource profile is materially different:

| Implementation | CPU avg | Memory avg |
|---|---:|---:|
| Hot Chocolate + EF Core | 176.3% | 165.9 MB |
| Foundgine — no cache | 75.0% | 88.4 MB |
| Foundgine — provider-plan cache | 84.3% | 85.9 MB |

So the right conclusion is not "Foundgine wins mutations". The defensible conclusion is:

> **Foundgine mutation execution is competitive in throughput on this workload while using substantially fewer measured API-container resources, but Hot Chocolate + EF Core remains faster at the highest tested concurrency.**

---

## Finding 4 — Upsert + select is the most useful next performance target

The original benchmark label suggested an upsert, but the earlier implementation actually performed a `createCustomer` followed by the query. That made the workload unsuitable for a clean upsert comparison.

The harness is now corrected to perform:

```text
Customer 1..N
    │
    ├── real PostgreSQL upsert on CustomerKey
    │
    └── exact same top-50/full-graph query
```

The corrected benchmark should be treated as a new baseline. No new numerical conclusion is claimed here until it has been run.

This workload is valuable because it combines two real application costs:

1. write/upsert execution;
2. a full graph read/refetch immediately after the write.

That makes it a much more meaningful end-to-end workload than comparing an isolated mutation with an isolated query and assuming their costs add linearly.

The benchmark deliberately keeps the select side identical to `QueryTop50`, so the results can be compared directly with the standalone query baseline.

---

# 3. Cache opportunities beyond the current plan cache

The current benchmark proves that plan caching can matter, but it should not lead to the conclusion that "more plan caching" is the only next step.

There are several independent cache layers:

```text
Request
   ↓
Semantic / resolution cache
   ↓
Authorization / policy cache
   ↓
Execution-plan cache
   ↓
Provider-plan cache
   ↓
Database
   ↓
Result cache
   ↓
Serialized response cache
   ↓
HTTP / transport cache
```

## Result caching is a particularly important next experiment

A result cache can sit **after provider execution** and cache the result of an eligible semantic query.

For a repeated request such as:

```text
customer(first: 50) { ...full graph... }
```

an eligible result-cache hit could avoid:

- PostgreSQL execution;
- row retrieval;
- graph materialization;
- some result shaping work.

That is fundamentally different from a provider-plan cache hit.

A result cache also introduces harder engineering questions:

- authorization context must be part of cache identity where necessary;
- invalidation must be correct after mutations;
- data volatility affects usefulness;
- payload size affects memory and serialization cost;
- hit rate matters more than cache existence;
- stale-read policy must be explicit.

The next cache benchmark should therefore report **hit rate, miss latency, hit latency, throughput, CPU, memory and PostgreSQL load** rather than only RPS.

---

# 4. FASTER as another cache provider

The architecture should also leave room for a cache implementation backed by **FASTER**.

FASTER should be treated as an implementation experiment, not as a claim that it will automatically outperform the current in-process provider-plan cache.

The useful comparison is:

| Cache strategy | What it caches | Questions |
|---|---|---|
| No cache | Nothing | Baseline execution cost |
| Current provider-plan cache | Compiled provider plan | Does compilation dominate? |
| Result cache | Query result | How much DB work can be avoided? |
| Provider-plan + result cache | Both | Do the layers compound? |
| FASTER-backed cache | Depends on selected cache layer | What are throughput, memory and persistence characteristics? |

FASTER is particularly interesting if the cache needs a different performance or storage profile than a simple in-memory dictionary. It should be measured with the same workload and hit/miss distribution as the other strategies.

No FASTER implementation is part of this benchmark baseline yet.

---

# 5. What the benchmark does and does not prove

### It does prove useful workload observations

- The current Foundgine query path is very strong on the supplied PostgreSQL graph workload.
- The provider-plan cache provides a measurable query improvement at high concurrency.
- Foundgine mutation execution uses materially fewer measured API-container resources in the supplied mutation workload.
- Hot Chocolate + EF Core remains ahead on the supplied whole-graph mutation throughput at C=32.
- The corrected upsert + full-graph-select workload is the right next end-to-end comparison.

### It does not prove

- universal Foundgine performance superiority;
- lower total infrastructure cost;
- lower PostgreSQL resource usage;
- that provider-plan caching is always beneficial;
- that result caching will be beneficial for every workload;
- that FASTER will outperform the current cache;
- that SQL is the optimal provider for every workload.

---

# 6. Next experiments

The next performance cycle should be deliberately narrow and measurable.

## A. Rerun the corrected upsert + select

Compare:

- Hot Chocolate + EF Core
- Foundgine no cache
- Foundgine provider-plan cache

Across:

- concurrency 1, 8, 32
- batch 1, 10, 50
- identical full-graph select

## B. Add payload sensitivity

```text
small projection
medium projection
full graph
```

This will reveal whether the performance gap changes as result shaping and serialization become more important.

## C. Add result caching

Measure:

```text
no cache
plan cache
result cache
plan + result cache
```

Report cache hit/miss rates and PostgreSQL resource consumption.

## D. Add FASTER

Run the same cache matrix with a FASTER-backed implementation.

## E. Add stage-level timing

Container CPU tells us that one system is doing less work, but not exactly where the time goes. The next instrumentation layer should split:

```text
semantic resolution
      ↓
authorization
      ↓
planning
      ↓
provider compilation
      ↓
PostgreSQL execution
      ↓
result materialization
      ↓
serialization
```

That will turn the benchmark from a throughput comparison into an explanation of the performance characteristics.

---

# 7. Reproducibility rules

The benchmark should remain:

- correctness-gated;
- deterministic in fixture shape;
- isolated with a fresh PostgreSQL database per target;
- explicit about batch semantics;
- explicit about cache state;
- explicit about workload payload;
- measured long enough to expose tail behaviour;
- conservative about conclusions.

The benchmark is an engineering instrument, not a marketing guarantee.
