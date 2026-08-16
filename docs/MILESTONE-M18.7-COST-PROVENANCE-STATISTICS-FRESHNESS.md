# M18.7 — Cost Provenance + Statistics Freshness

M18.7 makes provider cost estimates explainable and freshness-aware.

## Cost estimate contract

A `ProviderCostEstimate` now carries `CostEstimateProvenance`:

- source
- statistics version
- estimated-at timestamp
- statistics age
- freshness state

The estimate remains advisory. Provenance does not grant a rewrite permission and cannot bypass semantic-equivalence or security-preservation proofs.

## Freshness

The registry distinguishes:

- `Unknown` — no statistics timestamp is available, including heuristic estimates
- `Fresh` — statistics are within the configured freshness window
- `Stale` — statistics exceed the configured freshness window
- `Aging` — reserved for future graduated freshness policies

The SQL provider defaults to an explicitly labelled `heuristic` source and does not pretend that synthetic estimates came from PostgreSQL statistics.

## SQL provider integration

`SqlCostModelOptions` can now describe a statistics source, version, observation timestamp, and stale-after interval. When supplied, the estimator records the resulting provenance and adjusts confidence conservatively for fresh versus stale statistics.

## Architectural rule

Provider statistics may influence plan selection, but they do not change semantic meaning or security requirements. Stale or unknown statistics are evidence-quality signals, not authorization signals.

## What M18.7 proves

- cost estimates have explicit provenance
- statistics versions can be tracked
- statistics age is measurable
- stale estimates are distinguishable from fresh estimates
- heuristic estimates are not misrepresented as database statistics

## What it does not prove

M18.7 does not claim that the SQL provider is connected to a live PostgreSQL statistics catalog. That requires a future statistics adapter and database integration milestone.
