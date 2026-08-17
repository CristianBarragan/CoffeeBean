# Run 5 — Fixed EF Core endpoint vs dynamic Foundgine batch endpoint

## Why this benchmark matters

Run 5 is deliberately **not** a claim that two identical endpoints have identical capabilities. It compares two different execution models because that difference is the product question:

- **EF Core baseline:** a fixed, operation-specific `transfer_funds` MCP endpoint. The endpoint accepts one `TransferFundsCommand` and executes the pre-written transfer workflow.
- **Foundgine:** a dynamic semantic endpoint. The `transfer_funds_batch` capability accepts an array of transfer commands and lets Foundgine lower the requested capability into one set-based PostgreSQL operation.

Both paths implement the same high-assurance transfer semantics: tenant isolation, authorization, frozen-account checks, available-funds and daily-limit validation, idempotency, audit, atomicity, and deterministic account locking. The benchmark therefore asks a useful systems question: **what happens when a fixed application endpoint is compared with a dynamic capability endpoint that can batch the logical work into one database execution?**

The difference in capabilities is intentional and must be disclosed when interpreting the results.

## EF Core locking note

EF Core does **not expose a portable first-class `FOR UPDATE` row-lock API** in its normal LINQ abstraction. The EF Core benchmark therefore had to be modified to accommodate the same high-assurance locking discipline used by the PostgreSQL implementation.

The EF baseline uses:

1. a PostgreSQL transaction;
2. the same transaction-scoped advisory lock for the idempotency key; and
3. raw SQL `SELECT ... FOR UPDATE` reads in deterministic account-id order before EF Core's tracked state transition.

EF Core still performs the mutation through change tracking and one `SaveChangesAsync()` call. This is therefore **not plain out-of-the-box EF Core**. It is an EF Core implementation augmented with explicit PostgreSQL locking so the comparison does not accidentally give either side a weaker concurrency contract.

This distinction is important: the row locks are PostgreSQL behaviour requested by the benchmark implementation, not an EF Core default.

## Run configuration

- Run: 5
- Fixture: 10 customers
- Concurrency: 64
- Warmups: 5
- Measured runs: 30
- Operations per Foundgine batch: 64
- Successful operations: 100% on both paths
- Transport: MCP on both paths
- Database: PostgreSQL

## Results — 10 customers, concurrency 64

| Metric | MCP + EF Core | MCP + Foundgine UNNEST batch |
|---|---:|---:|
| Average throughput | **980.1 ops/s** | **10,731.0 ops/s** |
| Throughput advantage | — | **10.95× / +994.8%** |
| Average request/batch wall time | **45.3 ms** | **180.5 ms** |
| Effective amortized operation latency | **45.3 ms** | **~2.82 ms** |
| p50 | **36.3 ms** | **129.5 ms / batch** |
| p95 | **118.9 ms** | **401.9 ms / batch** |
| p99 | **126.8 ms** | **520.7 ms / batch** |
| Failures | 0 | 0 |

The effective Foundgine operation latency is calculated as batch wall time divided by 64 operations. The raw batch latency must not be compared directly with EF Core's single-transfer latency: one Foundgine MCP call carries 64 logical transfers while one EF Core MCP call carries one.

## What the result actually demonstrates

Foundgine is processing **64 logical transfer operations as one database-oriented batch**, rather than paying the full MCP/application/database execution path once per transfer.

At this concurrency and fixture tier:

- EF Core: ~980 individual transfer operations/second.
- Foundgine: ~10,731 logical transfer operations/second.
- The observed throughput improvement is **~10.95×**.
- The Foundgine batch's average wall time is ~180.5 ms, but that represents 64 logical operations, giving ~2.82 ms amortized per operation.

The strongest claim is therefore not "Foundgine is 11× faster than EF Core" in the abstract. It is:

> **A dynamic Foundgine capability that can batch the logical mutation into one PostgreSQL set operation can process roughly 11× as many logical transfers per second as this fixed, single-transfer EF Core endpoint under the tested workload.**

## Why the locking detail matters

The benchmark intentionally gives the EF Core baseline explicit row locking so both implementations enforce the same high-assurance concurrency semantics. That makes the comparison more meaningful, but it also means the EF number is not representative of a vanilla EF Core application that simply calls `SaveChangesAsync()` without explicit row locks.

Conversely, Foundgine's advantage is not only its SQL engine. The dynamic endpoint changes the unit of work: 64 commands arrive together and are lowered into a set-oriented database execution. That capability is unavailable to the fixed EF endpoint being tested.

## Tail latency remains a separate concern

The throughput result is strong, but the batch tail is not trivial. Foundgine's p95/p99 are measured over **whole 64-operation batches**, not individual operations. The benchmark should therefore continue to track both:

- batch wall-time percentiles; and
- amortized logical-operation latency.

Future runs should also vary batch size (1, 8, 16, 32, 64, 128, 256, 512, 1024) to show where batching stops scaling and whether tail latency grows faster than throughput.
