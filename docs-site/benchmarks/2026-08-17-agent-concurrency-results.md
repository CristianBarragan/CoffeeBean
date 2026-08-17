# Agent End-to-End Concurrency Results — 17 August 2026

## Benchmark

- Scenario: `customer-exposure-review`
- Mode: replay
- Fixture: 10 customers
- Measured runs: 30 per concurrency tier
- Warmups: 5
- Fixture customer: 1
- Relationships: 4
- Contracts: 12
- Transactions: 48
- Exposure: 48,240.00

## Results

| Concurrency | Conventional avg wall | Foundgine avg wall | Conventional effective flow/s | Foundgine effective flow/s |
|---:|---:|---:|---:|---:|
| 8 | 19.7 ms | 49.5 ms | 405.6 | 161.7 |
| 16 | 29.7 ms | 85.1 ms | 539.6 | 188.0 |
| 32 | 39.9 ms | 146.3 ms | 802.9 | 218.7 |
| 64 | 66.7 ms | 315.7 ms | 960.0 | 202.7 |

## Agent-loop findings

- Conventional: 7.0 tool calls/flow.
- Foundgine: 4.0 tool calls/flow.
- Tool-call reduction: 42.9%.
- Estimated context-load saving: 43.4–43.5%.
- Provider token savings: not measured because this is replay mode.

## Correctness status

The original benchmark reported `Same final state: False`, but that check was not a valid semantic equivalence test. It compared the complete serialized snapshot across all runs, including `CustomerId` and `CustomerKey`; concurrent flows can legitimately operate on different customers, so their snapshots are expected to differ.

The benchmark has now been changed to generate an explicit `expected-state.json` after resetting the fixture and to compare every observed final snapshot against the expected behavior for that customer. The verification checks: CustomerId, CustomerKey, reviewed FullName, relationship count, contract count, transaction count, and exposure. A new `verification` section is written into `agent-benchmark.json` and `agent-benchmark.md`. The corrected correctness result must be obtained by rerunning the benchmark.

## Latency status

Foundgine was slower in this replay configuration. Average wall-time was approximately 2.5×, 2.9×, 3.7× and 4.7× the conventional path at concurrency 8, 16, 32 and 64 respectively.

## Concurrency caveat

The fixture has 10 customers. At concurrency 16, 32 and 64, customer IDs are reused across concurrent flows. These tiers therefore represent concurrency stress, not independent-customer scaling.
