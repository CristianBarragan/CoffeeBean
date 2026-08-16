# Foundgine Agent End-to-End Benchmark — customer-exposure-review

Generated: `2026-08-16T09:27:53.7318062+00:00`  
Mode: `replay`  
Runs: `10` measured / `3` warmups

## Headline

- Input-token saving: **0.0%**
- Total-token saving: **0.0%**
- Tool-call saving: **42.9%**
- Model-call saving: **0.0%**
- Wall-clock change: **156.4%**
- Same final state: **True**

## Measured averages

| Metric | Conventional | Foundgine |
|---|---:|---:|
| Wall clock (ms) | 9.1 | 23.4 |
| Model time (ms) | 0.0 | 0.0 |
| Tool time (ms) | 6.9 | 21.1 |
| Model calls | 0.0 | 0.0 |
| Tool calls | 7.0 | 4.0 |
| Input tokens | 0.0 | 0.0 |
| Output tokens | 0.0 | 0.0 |
| Total tokens | 0.0 | 0.0 |
| Cached input tokens | 0.0 | 0.0 |

## Method

Both flows run against the same PostgreSQL fixture, the same authenticated benchmark request, the same deterministic Customer 1 graph, and the same final-state assertion. Live mode records provider-reported usage; replay mode is for validating the harness and must not be presented as real model-token evidence.

## Scenario

Find the highest-exposure eligible customer for the authenticated tenant with exposure above the configured threshold, then perform the authorized review mutation and verify the final state. The benchmark compares the conventional discovery/tool choreography against the Foundgine semantic capability choreography while asserting the same final state.
