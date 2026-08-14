# Testing

Tests are the executable definition of the current architecture.

## Run everything

```bash
dotnet test
```

## Test layers

- `Foundgine.Semantics.Tests` — semantic model and authorization.
- `Foundgine.Planning.Tests` — planning boundaries.
- `Foundgine.Aot.Tests` — generated metadata.
- `Foundgine.Intent.Json.Tests` — JSON input boundary.
- `Foundgine.GraphQL.HotChocolate.Tests` — GraphQL adapter behaviour.
- `Foundgine.E2E.Tests` — complete runtime paths.

## What to test

Prefer tests at the narrowest useful boundary, then add an end-to-end test when a capability crosses layers.

Do not weaken a test just to make an implementation pass. Change the implementation when the contract is correct; change the test when the contract itself was wrong.

## GitHub merge gates

The default GitHub Actions workflow treats the repository as a gated build, not just a packaging job.

Every pull request runs four explicit checks:

1. **Build** — restores and builds the complete solution in Release configuration.
2. **Unit Tests** — rebuilds the solution and executes the full unit/integration test suite. Test results are uploaded as a workflow artifact.
3. **Performance** — starts a fresh PostgreSQL fixture and runs the Foundgine performance smoke/regression workload. It fails on request errors/timeouts and on conservative throughput/latency guardrails for query, whole-graph mutation and upsert+select.
4. **Package** — rebuilds and packs all expected NuGet packages, then validates package contents, README packaging and the AOT analyzer layout.

`Unit Tests`, `Performance`, and `Package` run only after `Build` succeeds. A tagged release publishes NuGet packages only after all four checks pass.

The performance job is deliberately a **merge guardrail**, not the published benchmark. The full benchmark remains under `benchmarks/CoffeeBeanery.Performance` and should be used for performance analysis and baseline updates.

### Branch protection

To make these checks actually block merging, configure the repository's default branch protection/ruleset to require these status checks:

- `Build`
- `Unit Tests`
- `Performance`
- `Package`

Also require the pull request to be up to date with the protected branch if that is part of the repository's normal merge policy.

A failing build, unit test, performance gate or package validation should therefore prevent the pull request from being merged.

### Current performance guardrails

The CI gate currently uses these conservative checks at concurrency 8:

| Workload | Guardrail |
|---|---|
| Top-50 query | ≥ 1,000 RPS and p95 ≤ 100 ms |
| Whole-graph mutation, batch 50 | ≥ 10,000 logical/s and p95 ≤ 500 ms |
| Upsert + select, batch 10 | ≥ 20 RPS and p95 ≤ 1,000 ms |

These values are intentionally well below the published benchmark baseline. They are meant to catch catastrophic regressions, broken execution paths and latency explosions rather than reject ordinary CI-run-to-run variance.
