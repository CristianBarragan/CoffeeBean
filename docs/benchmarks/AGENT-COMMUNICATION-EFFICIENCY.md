# Agent communication efficiency benchmark

## Why this test matters

Foundgine should not be evaluated only as a faster implementation of a fixed endpoint. Its differentiating capability is a **dynamic semantic execution boundary**: an agent can ask for a business capability and the runtime can resolve relationships, authorization, planning and execution without requiring the agent to reconstruct the application's physical state through a sequence of narrow calls.

The communication benchmark therefore measures:

1. **Agent/tool round trips** — recorded tool invocation + response interactions.
2. **Tool payload bytes** — UTF-8 bytes of tool input plus output.
3. **Estimated context load** — the existing directional token estimator.
4. **Provider-reported tokens** — only when live model usage is available.
5. **End-to-end wall time** — measured separately from the database batch benchmark.

## Current Run 2 evidence

Configuration: 10 customers, concurrency 64, 30 measured runs, 5 warmups, replay mode.

| Metric | Conventional fixed tools | Foundgine dynamic capability | Change |
|---|---:|---:|---:|
| Agent/tool round trips | 9.0 | 6.0 | **−33.3%** |
| Tool payload | ~3,437 bytes | ~2,166 bytes | **−37.0%** |
| Estimated context load | 1,166 tokens | 858 tokens | **−26.5%** |

The ~26.5% context reduction is the measured result that should be described as **about 27%** when communicating the headline.

## Capability distinction

The conventional side is intentionally a collection of narrow, fixed application tools. Foundgine is intentionally dynamic. Therefore the benchmark compares the same **business outcome**, not identical API shapes.

A fixed endpoint may be preferable when the operation is already known and stable. Foundgine's value appears when an agent must discover and execute a richer, stateful capability that spans multiple entities and execution steps.

## Trust requirements

The benchmark must not imply that:

- estimated tokens are provider billing data;
- tool round trips equal all network round trips;
- a dynamic capability is always faster than a hand-written fixed endpoint;
- the database throughput benchmark and the agent communication benchmark measure the same thing.

The strongest evidence comes from the combination:

**fewer interactions + smaller payload + preserved correctness + lower context load + higher set-oriented execution throughput.**

The next validation step is a live provider-backed run that records actual model usage and end-to-end latency while preserving the same workload, tool contracts and correctness assertions.
