# Foundgine Agent End-to-End Benchmark

This benchmark compares the same business process through two agent-facing architectures:

1. **Conventional** — physical/application discovery and multiple narrow tools.
2. **Foundgine** — semantic capability discovery, graph execution, semantic mutation, and verification.

Both flows use the same PostgreSQL fixture, authenticated benchmark customer, reset operation, and explicit expected-state assertions. The scenario is intentionally stateful: QUERY #1 → MUTATION #1 → QUERY #2 → MUTATION #2 → QUERY #3.

## Token methodology

The current benchmark methodology is intentionally preserved:

```text
estimated tokens = max(chars / 4, words × 1.3)
```

The estimator is applied to **every recorded tool input and every recorded tool output**. After those payloads are summed, the benchmark adds the fixed system prompt and scenario request once per run.

This is an offline BPE-style approximation. It is not a provider tokenizer and should not be presented as provider-reported usage. The benchmark therefore reports two distinct measurements:

- **Estimated context-load tokens** — the requested chars/words heuristic.
- **Provider-reported tokens** — actual API usage when `AGENT_BENCHMARK_MODE=live` and the model endpoint returns usage data.

The heuristic does **not** include the model's own reasoning tokens or model response tokens. This is deliberate and must remain visible in published reports.

## One-command performance runner

From the repository root:

```powershell
.\benchmarks\AgentEndToEnd\Run2\run-agent-end-to-end.ps1
```

The default matrix is:

| PostgreSQL customer volume | measured runs |
|---:|---:|
| 10 | 10 |
| 100 | 10 |
| 1,000 | 10 |
| 10,000 | 10 |

Each tier gets a **fresh PostgreSQL volume**, then the benchmark fixture is seeded with the exact configured graph density. This means the database volume is the intentional independent variable between tiers.

The customer under test remains **Customer 1** for every tier so the business request and graph shape are held constant while the total database volume changes.

### Common options

```powershell
# Change measured runs and warmups
.\benchmarks\AgentEndToEnd\Run2\run-agent-end-to-end.ps1 `
    -CustomerCounts 10,100,1000,10000 `
    -RunsPerTier 30,30,30,30 `
    -Warmups 5

# Use a custom volume/run matrix
.\benchmarks\AgentEndToEnd\Run2\run-agent-end-to-end.ps1 `
    -CustomerCounts 10,100,1000 `
    -RunsPerTier 10,20,30

# Keep the PostgreSQL volume after the run
.\benchmarks\AgentEndToEnd\Run2\run-agent-end-to-end.ps1 -KeepDatabase
```

### Live model mode

```powershell
$env:AGENT_BENCHMARK_MODE="live"
$env:AGENT_MODEL_ENDPOINT="https://.../v1/chat/completions"
$env:AGENT_MODEL_API_KEY="..."
$env:AGENT_MODEL="..."

.\benchmarks\AgentEndToEnd\Run2\run-agent-end-to-end.ps1 `
    -CustomerCounts 10,100,1000 `
    -RunsPerTier 10,10,10 `
    -Warmups 3
```

Live mode records provider-reported prompt/input, completion/output, total, and cached-input usage when exposed by the compatible endpoint. Replay mode records no provider token usage and exists to validate the choreography and application measurements.

## What is held constant

Across each volume tier:

- PostgreSQL major version
- database schema and indexes
- graph density per customer
- authenticated benchmark request
- Customer 1 as the measured subject
- exposure calculation
- mutation target
- final-state assertion
- Conventional and Foundgine comparison logic
- warmup policy
- measured-run policy

Only the **total number of customers and therefore total graph volume** changes.

## What the runner produces

Each tier gets its own report directory:

```text
benchmarks/AgentEndToEnd/Run2/artifacts/
├── 00010-customers/
│   ├── agent-benchmark.json
│   └── agent-benchmark.md
├── 00100-customers/
│   ├── agent-benchmark.json
│   └── agent-benchmark.md
├── 01000-customers/
│   ├── agent-benchmark.json
│   └── agent-benchmark.md
└── 10000-customers/
    ├── agent-benchmark.json
    └── agent-benchmark.md
```

The benchmark itself reports:

- estimated context-load tokens
- provider-reported tokens in live mode
- model calls
- tool calls
- model time
- tool/application time
- wall-clock time
- expected-state verification
- intermediate-state verification inside the replay choreography
- final-state correctness
- the full query → mutation → query → mutation → query process

The token estimate is **not** a claim about model reasoning tokens.

## Clean checkout / first run

The runner performs an explicit NuGet restore for the AgentEndToEnd and
CoffeeBeanery.Database projects before using `--no-restore`. No pre-existing
`obj/project.assets.json` files are required.

The benchmark uses the local `docker-compose.yml` in this directory and does
not depend on the CoffeeBeanery benchmark compose file.
