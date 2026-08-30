# Performance and benchmark evidence

Foundgine performance claims are scoped to explicit workloads. The benchmark suite separates measured RPS, latency, tool calls and success/failure counts from estimated context metrics.

The strongest current agent-facing evidence concerns reduced tool coordination and semantic batching. The TransferFunds run intentionally records a concurrency limitation rather than hiding it; the same-client follow-up isolates request shape and demonstrates the benefit of one semantic batch call.

PostgreSQL query measurements are also workload-specific and should not be treated as a universal comparison against every ORM, schema or hardware configuration.
