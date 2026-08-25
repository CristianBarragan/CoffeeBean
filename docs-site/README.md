# Foundgine docs site

## Current structure

The site is organized around the product story:

1. Problem / intention
2. What Foundgine is
3. Getting started (hands-on Supply Chain sample tutorial, follows the sample's GUIDE.md)
4. How it works
5. Architecture
6. AI agents
7. Agent benchmark explorer
8. Performance evidence

Milestone/phase pages are not part of the public site structure.

## Benchmark explorer

`agent-benchmark/index.html` is the canonical benchmark page. The landing area opens a sliding run menu containing Run 1 through Run 5. Each run item explains the purpose, experiment, finding and meaning of that run. Selecting a run updates `?run=N` and moves to the corresponding evidence section.

The matrix remains a secondary navigation/evidence surface. Runs 2, 4 and 5 have published workload/concurrency matrices in the current assets.

## Telemetry

The static site does not expose secrets or host sensors. `telemetry-api/` is a .NET 9 bridge that reads:

- Scaphandre power metrics
- Electricity Maps grid carbon intensity
- provider/model token usage posted by the benchmark runner

The browser polls the bridge every 10 seconds. If the bridge is unavailable it never invents power or CO₂ values.
