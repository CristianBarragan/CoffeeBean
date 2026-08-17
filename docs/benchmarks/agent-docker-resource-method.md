# Agent Benchmark Docker Resource Measurement

The Agent End-to-End benchmarks now collect container-level Docker telemetry.

## Metrics

At approximately one-second intervals, the collector records:

- CPU percentage
- memory usage and memory limit
- memory percentage
- network receive/transmit counters
- block I/O read/write counters
- process count (PIDs)
- UTC timestamp
- Docker service and container identity

Raw samples are retained as CSV and a compact summary is generated as JSON.

## Interpretation

These measurements describe application/infrastructure resource consumption.
They are separate from LLM token usage and provider billing.

A total agent cost model should therefore be calculated as:

`TotalCost = ModelCost + ApplicationComputeCost + DatabaseCost + NetworkCost`

and an efficiency measure can use:

`CostEfficiency = SuccessfulOperations / TotalCost`

`ResourceEfficiency = SuccessfulOperations / CPUSeconds`

`MemoryEfficiency = SuccessfulOperations / GBSeconds`

Docker CPU and memory statistics alone are **not** a direct measurement of
watts or watt-hours. For measured energy, collect host/server power telemetry
(e.g. a platform power meter or supported CPU energy counters) during the same
benchmark window.
