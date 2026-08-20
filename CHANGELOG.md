# Changelog

All notable changes to this repository are documented here. This file starts at 0.4.0 — no changelog was kept for earlier versions, so 0.1.0–0.3.0 are not reconstructed here.

## [Unreleased]

### Added
- **M18.9 — Projection Pruning.** The planner includes a conservative projection-pruning rule that removes redundant duplicate fields without changing requested field order. Fields required by filters and ordering are tracked explicitly, and every accepted rewrite continues through semantic-equivalence and security-preservation proofs. The current semantic model intentionally does not remove unique requested fields, because output and working projections are not yet represented separately — that stronger dead-field optimization is reserved for a future projection-dependency milestone.
- **M18.11 — Join Ordering / Multi-Relationship Planning.** Adds conservative cardinality- and selectivity-aware traversal ordering metadata for sibling relationship plans. Logical child order remains unchanged; providers may use `TraversalOrder` for physical planning subject to semantic and security conformance.

## [0.4.0]

### Added
- `benchmarks/AgentEndToEnd/scripts/estimate_cost_savings.py` — offline $ savings estimator built on the existing `estimate_tokens.py` heuristic. Converts the per-run token-load estimate into $/call, $/day, $/month, $/year at a chosen call volume and model price. Handles both the nested `Flows` report shape and the flat `Results` shape the .NET harness actually writes.
- `docs-site/agent-benchmark/index.html`:
  - Live "Estimated $ savings at scale" table, rendered from the same benchmark report as the existing token-load estimate.
  - "What if this ran at data-center scale?" section — a napkin-math projection of the measured token-load reduction against public 2026 data-center energy figures (IEA/Gartner), with every assumption stated as a table.
  - "Guardrails: efficiency is not the same as autonomy" section, tying the benchmark's efficiency numbers back to authorization, narrow mutation intent, mandatory post-mutation verification, and the same-final-state correctness gate.
  - "If this became the default pattern: a 50-year projection" (`#fifty-year-projection`) — a long-horizon, explicitly-labeled scenario (not a forecast) projecting cumulative electricity, dollar, and CO₂e impact under conservative/base/aggressive adoption assumptions, with a full assumptions table.
- `docs-site/index.html` — homepage callouts surfacing the headline benchmark numbers (tool-call and token-load reduction, $/month at scale) and the 50-year scenario's headline range, linking into the full detail and methodology on the benchmark page.
- `devto-article.md`, `linkedin-post.md` — external write-ups of the benchmark result, its cost/energy implications, and the guardrails point, with the same caveats carried through.

### Fixed
- `docs-site/assets/agent-benchmark.js` — the live token-estimate box read `report.Flows`, which does not exist in the report the .NET harness actually produces (it writes a flat `Results` array with a `Flow` field per run). This silently zeroed out the on-page estimate. Added an adapter (`toFlows()`) that builds the expected shape from either report layout.

### Changed
- `VersionPrefix` bumped `0.1.0` → `0.4.0` in `Directory.Build.props`.
