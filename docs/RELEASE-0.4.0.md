# Foundgine 0.4.0

Foundgine 0.4.0 has been superseded by [Release 0.5.0](RELEASE-0.5.0.md). It carries forward the 0.3.0 release gates and core release surface (see [Release 0.3.0](RELEASE-0.3.0.md)) and adds the plan-rewrite optimizer work and the benchmark evidence/reporting work described below. See [CHANGELOG.md](../CHANGELOG.md) for the itemized change list.

## Release gates

The current source tree has passed:

```text
dotnet restore   ✓
dotnet build     ✓
dotnet test      ✓
```

The repository also contains separate PostgreSQL integration and CoffeeBeanery benchmark workflows. Those require their own database/runtime environment and should be reported separately from the normal solution test gate.

## What's new since 0.3.0

- **Plan-rewrite optimizer suite.** The planner (`src/Foundgine.Planning`) now includes a rewrite-rule series: provider-aware cost estimation and rule selection, predicate pushdown, projection pruning, relationship traversal and join ordering, and aggregate/cardinality-aware rewrites (existence collapse, relationship filter pushdown, null/empty/duplicate semantics). Every accepted rewrite is gated by semantic-equivalence and authorization-preservation proofs — the optimizer cannot change results or weaken authorization. See [docs/ROADMAP.md](ROADMAP.md) for each rewrite rule's design rationale.
- **Benchmark cost and scale evidence.** `benchmarks/AgentEndToEnd/scripts/estimate_cost_savings.py` converts measured token-load reduction into $/call, $/day, $/month, and $/year at a chosen call volume and model price. The benchmark site now surfaces this as a live "Estimated $ savings at scale" table alongside a data-center-scale energy projection and an explicitly-labeled long-horizon adoption scenario, all with assumptions stated in tables rather than asserted.
- **Guardrails framing on the benchmark page.** The efficiency numbers are now tied explicitly back to authorization, narrow mutation intent, mandatory post-mutation verification, and the same-final-state correctness gate, so the efficiency story isn't read as an autonomy claim.
- **Fixed a live benchmark display bug.** The on-page token-load estimate silently read a report shape (`report.Flows`) that the .NET harness doesn't actually produce, zeroing out the estimate. An adapter now normalizes either report shape.

## Core release surface

Everything listed in [Release 0.3.0](RELEASE-0.3.0.md#core-release-surface) remains true, plus:

- provider-neutral rewrite cost/benefit estimation and deterministic rewrite-rule selection;
- predicate pushdown, projection pruning, relationship traversal/join ordering, and aggregate/cardinality-aware plan rewrites, each gated by semantic-equivalence and authorization-preservation proofs;
- authorization-predicate canonicalization (deterministic normalization, duplicate elimination, commutative canonicalization) feeding deterministic plan fingerprints.

## Deliberate non-claims

0.4.0 does not change the 0.3.0 non-claims. Foundgine is still not an autonomous-agent runtime, a workflow/orchestration engine, a universal provider abstraction with full cross-provider feature parity, an ORM replacement, an identity/authorization provider, or a universally faster alternative to EF Core, GraphQL servers, or other execution stacks.

## Evidence policy

Behavioral claims should be backed by active tests. Performance claims should identify the workload, fixture, concurrency, provider versions, and measurement method. Historical phase documents explain prior design decisions but are not release specifications.
