# Foundgine Performance Benchmark — 2026-08-15 (corrected)

## Correction notice

**This replaces an earlier 2026-08-15 version of this report that was wrong and must not be used.**

The previous version of this file claimed Hot Chocolate + EF Core was ~54× faster than Foundgine on
the top-50 query workload at concurrency 32 (11,728.4 RPS vs. 217.4/151.7 RPS). No raw CSV/JSON backing
that run is checked into the repository, and the claim contradicts every other benchmark run on record,
including the 2026-08-13 baseline and the run below. The most likely explanation is that the Foundgine
and Hot Chocolate columns were transposed when that report was written, or it was transcribed from a
broken/misconfigured run. Either way, it should be treated as retracted, not as a regression that was
later fixed.

The tables below are rebuilt directly from a full benchmark log (`hotchocolate`, `foundgine-cold`,
`foundgine-warm` targets, timestamps 2026-08-15 03:42–03:59) and match the 2026-08-13 baseline's overall
shape: **Foundgine is substantially faster than Hot Chocolate + EF Core on the query workload**, and is
competitive-to-ahead on mutation and upsert+select throughput.

## Executive summary

This run compares the same PostgreSQL-backed CoffeeBeanery workload across three targets:

- Hot Chocolate + EF Core
- Foundgine — no cache (`foundgine-cold`)
- Foundgine — provider-plan cache (`foundgine-warm`)

Fixture: 1,000 customers, 4,000 relationships, 12,000 contracts, 48,000 transactions. Warm-up 3s,
measurement 10s, concurrency 1/8/16/32/64, mutation and upsert batch sizes 1/10/50. All rows below
completed with `errors=0, timeouts=0, cancelled=0`.

For batched mutation/upsert workloads, HTTP RPS and **logical/s** (HTTP RPS × batch size) are reported
separately, since one HTTP request represents multiple logical operations.

## Query — top 50 graph

| Concurrency | Hot Chocolate RPS | Foundgine (no cache) RPS | Foundgine (cache) RPS |
|---:|---:|---:|---:|
| 1  | 32.00  | 339.40  | 468.50  |
| 8  | 208.70 | 1,669.10 | 2,052.90 |
| 16 | 221.90 | 1,707.70 | 2,546.60 |
| 32 | 224.00 | 2,352.20 | 2,975.70 |
| 64 | 223.20 | 2,871.60 | 3,079.90 |

| Concurrency | Target | p50 | p95 | p99 |
|---:|---|---:|---:|---:|
| 32 | Hot Chocolate + EF Core | 141.8 ms | 187.0 ms | 216.4 ms |
| 32 | Foundgine — no cache | 12.5 ms | 25.2 ms | 34.2 ms |
| 32 | Foundgine — provider-plan cache | 9.9 ms | 18.4 ms | 23.0 ms |

At concurrency 32, Foundgine's cached query path completed roughly **13.3× the HTTP request throughput**
of Hot Chocolate + EF Core, with p95 latency about **10× lower**. The uncached path is close behind at
roughly **10.5×**. Hot Chocolate's throughput also plateaus and its latency keeps climbing from C=16
onward (p50 goes from 67 ms to 294 ms between C=16 and C=64), while both Foundgine configurations keep
scaling RPS through C=64.

This matches the 2026-08-13 baseline's conclusion, not the retracted report's.

## Whole-graph mutation

### HTTP RPS by concurrency and batch size

| Batch | Concurrency | Hot Chocolate | Foundgine (no cache) | Foundgine (cache) |
|---:|---:|---:|---:|---:|
| 1  | 1  | 299.70 | 262.40 | 288.00 |
| 1  | 8  | 697.00 | 783.50 | 745.20 |
| 1  | 16 | 563.10 | 759.90 | 784.60 |
| 1  | 32 | 715.60 | 781.40 | 778.80 |
| 1  | 64 | 756.90 | 815.50 | 811.70 |
| 10 | 1  | 42.70  | 198.40 | 215.80 |
| 10 | 8  | 228.80 | 540.70 | 449.60 |
| 10 | 16 | 339.30 | 581.00 | 544.10 |
| 10 | 32 | 365.60 | 579.30 | 595.40 |
| 10 | 64 | 297.80 | 593.20 | 560.40 |
| 50 | 1  | 5.50   | 20.40  | 19.60  |
| 50 | 8  | 29.80  | 154.40 | 154.00 |
| 50 | 16 | 58.10  | 256.20 | 261.10 |
| 50 | 32 | 91.00  | 259.80 | 263.10 |
| 50 | 64 | 97.20  | 248.00 | 269.60 |

### Logical mutations/s at concurrency 32

| Batch | Hot Chocolate | Foundgine (no cache) | Foundgine (cache) |
|---:|---:|---:|---:|
| 1  | 715.6  | 781.4  | 778.8  |
| 10 | 3,656  | 5,793  | 5,954  |
| 50 | 4,550  | 12,990 | 13,155 |

At batch 1, mutation throughput is roughly comparable across all three targets — this is a single-row
insert per request and isn't where Foundgine's batched-mutation compiler has room to help. At batch 10
and batch 50, where Foundgine's PostgreSQL batched-mutation path (a single `unnest`/`MERGE` statement,
see `PostgresBatchedMutationCompiler`) actually applies, Foundgine pulls ahead by roughly **1.6–2.9×** in
logical throughput, and Hot Chocolate's batch-50 latency degrades sharply at high concurrency (p99 goes
from 723 ms at C=1 to 1,053 ms at C=64).

The provider-plan cache's effect on mutation throughput is small and inconsistent at this batch/concurrency
mix (sometimes slightly ahead of no-cache, sometimes slightly behind) — consistent with the 2026-08-13
finding that plan caching is not the dominant cost for mutation execution.

## Upsert + select (upsert then re-query full graph)

### HTTP RPS by concurrency and batch size

| Batch | Concurrency | Hot Chocolate | Foundgine (no cache) | Foundgine (cache) |
|---:|---:|---:|---:|---:|
| 1  | 1  | 44.60  | 119.50 | 154.90 |
| 1  | 8  | 177.00 | 417.60 | 721.90 |
| 1  | 16 | 199.70 | 450.40 | 703.60 |
| 1  | 32 | 199.70 | 613.30 | 659.10 |
| 1  | 64 | 203.10 | 674.10 | 672.60 |
| 10 | 1  | 22.80  | 146.10 | 142.90 |
| 10 | 8  | 104.40 | 457.00 | 482.80 |
| 10 | 16 | 113.10 | 470.80 | 397.80 |
| 10 | 32 | 146.50 | 488.50 | 450.40 |
| 10 | 64 | 147.00 | 438.40 | 502.50 |
| 50 | 1  | 5.90   | 18.00  | 18.50  |
| 50 | 8  | 32.60  | 143.10 | 141.90 |
| 50 | 16 | 50.90  | 246.70 | 225.90 |
| 50 | 32 | 71.40  | 236.90 | 245.30 |
| 50 | 64 | 82.70  | 257.20 | 248.70 |

At concurrency 32, batch 1, Foundgine is roughly **3.1–3.3×** faster than Hot Chocolate on this combined
write-then-refetch workload; at batch 50 the gap is roughly **3.3–3.4×**. The provider-plan cache's effect
here is again small relative to the gap over Hot Chocolate — most of the win comes from query/mutation
execution, not plan-lookup avoidance.

## Interpretation

1. **Query performance is a clear Foundgine strength in this workload**, at every tested concurrency —
   not a weakness. The retracted report's opposite conclusion should not be cited going forward.
2. **Batched mutation is where Foundgine's advantage is largest.** At batch 1 the targets are close; the
   gap opens up specifically at batch 10/50, where the single-statement batched compiler avoids per-row
   round trips that Hot Chocolate + EF Core still pays.
3. **The provider-plan cache is a real but secondary effect.** It reliably helps the query workload
   (roughly +5–27% RPS depending on concurrency) but has a small, sometimes negative, effect on mutation
   and upsert throughput at this batch/concurrency mix. This still points at the same next step as the
   2026-08-13 report: instrument semantic resolution, planning, SQL generation, execution, and
   serialization individually rather than assuming more caching layers will close remaining gaps.

## Benchmark limitations

These results are from the current development environment and workload (Docker on the CI/dev host used
to produce the linked JSON/CSV/MD reports below). Hardware, Docker configuration, PostgreSQL
configuration, runtime version, concurrency, payload size, and fixture shape all affect the numbers. CPU
and memory were not captured in this particular run (all rows report `CPU avg/max=0.0%/0.0%`,
`MEM avg/max/end=0.0/0.0/0.0MB`) — treat that as "not measured in this run," not as "zero resource cost."

Treat this as a reproducible engineering baseline, not a universal or marketing performance claim.

## Source reports

- `reports/query/hotchocolate/benchmark-Hot-Chocolate---EF-Core-20260815-034201.{json,csv,md}`
- `reports/query/foundgine-cold/benchmark-Foundgine---no-cache-20260815-035033.{json,csv,md}`
- `reports/query/foundgine-warm/benchmark-Foundgine---provider-plan-cache-20260815-035907.{json,csv,md}`

## E2E status

The PostgreSQL E2E suite is tracked separately from performance results. A passing E2E suite establishes
correctness; it does not alter these performance measurements.
