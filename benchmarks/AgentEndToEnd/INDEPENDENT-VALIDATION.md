# Independent benchmark validation

This is the shortest path for an external reviewer (including MAF) to reproduce the benchmark evidence.

## Prerequisites

- Windows PowerShell 5.1 or PowerShell 7+
- .NET 9 SDK
- Python 3.x
- Docker Desktop for the PostgreSQL/Foundgine fixture
- Git

From the repository root:

```powershell
.\benchmarks\AgentEndToEnd\validate-benchmark-harness.ps1
```

This validates the published JSON artifacts, rebuilds the canonical reports, builds the benchmark project, and validates Docker Compose when those tools are available.

## Deterministic replay

No model/API key is required:

```powershell
.\run-agent-benchmark.ps1 -Mode replay -Warmups 1 -Runs 3 -Publish
```

Replay validates the benchmark choreography and final state. It does **not** provide real model token usage.

## Live model run

Use an OpenAI-compatible chat-completions endpoint:

```powershell
$env:AGENT_MODEL_ENDPOINT = "https://your-compatible-endpoint/v1/chat/completions"
$env:AGENT_MODEL_API_KEY = "..."
$env:AGENT_MODEL = "your-model"

.\run-agent-benchmark.ps1 -Mode live -Warmups 1 -Runs 3 -Publish
```

The live report records provider-reported input/output/total tokens when the endpoint supplies them. The report also retains the heuristic context estimate separately.

For publishable evidence, use the same model, endpoint configuration, prompts, fixture, warmups and measured runs for both flows.

## MAF integration

The current harness compares:

```text
Conventional agent → application tools
Foundgine agent    → Foundgine semantic tools
```

It does not claim to be MAF until an actual MAF agent invokes the Foundgine capability.

The intended MAF validation is:

```text
MAF agent/workflow
       ↓
Foundgine capability
       ↓
semantic validation + authorization + planning
       ↓
provider
```

The same scenario and reporting schema can then be used for an independent MAF-side run. This avoids changing the workload merely to make the integration look favorable.

## Published artifacts

After a run, the canonical publishing command is:

```powershell
.\benchmarks\AgentEndToEnd\publish-all-reports.ps1
```

The canonical assets are under:

```text
docs-site\assets\agent-benchmark\
```

including `run1-aggregate.json` through `run5b-aggregate.json`, `benchmark-matrix.json`, and the separate Supply Chain report.

## Measurements vs estimates

The reports distinguish:

- measured latency/RPS/tool calls/success/failure
- provider-reported live token usage
- heuristic estimated context tokens
- illustrative estimated cost
- illustrative estimated energy

Estimated cost and energy are not presented as provider billing or measured electrical consumption.
