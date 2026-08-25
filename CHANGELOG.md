# Changelog

## [1.1.0] - 2026-08-26

### Added
- Added the secure `FoundgineHotChocolateQueryExecutor` in the separate `Foundgine.GraphQL.HotChocolate.Execution` package so the pure GraphQL adapter remains independent of `Foundgine.Execution`.
- Added the shared `ISecurityExecutionContextProvider` contract, delegate adapter, and fail-closed requirement helper under `Foundgine.Semantics.Security.Execution`.
- Added GraphQL executor coverage for missing security context, trusted host context propagation, result shaping, and stable adapter errors.

### Changed
- Merged the GraphQL query-executor step into the 1.0.0 source tree and separated the execution integration from the pure GraphQL adapter after boundary verification.
- Updated MCP to converge on the shared host-owned security execution-context provider while retaining its existing delegate compatibility path.
- Updated the Supply Chain getting-started sample to use repository `src/` project references instead of obsolete 0.5.x NuGet package instructions.
- Bumped the repository/package version to 1.1.0.

### Verification
- Static source/project-reference validation completed for the merged release tree.
- Full `dotnet build` / `dotnet test` verification is performed as part of the release gate; the initial package validation exposed two contract/boundary test failures which were fixed before repackaging.

## [1.0.0]

### Changed
- **`VersionPrefix` bumped `0.5.0` → `1.0.0` in `Directory.Build.props`.** This is a semver-stability declaration, not a runtime capability change: no `src/` package's implementation changed in this release. It marks the public API surface documented in [RELEASE-0.5.0.md](docs/RELEASE-0.5.0.md) (carried forward unchanged) as the first release under a `1.x` stability commitment.

### Added
- `docs-site/getting-started/` — a hands-on "Getting started" tutorial page that runs the `Foundgine.SupplyChain` sample end to end and walks through its ten architectural layers (API → Application → Domain → AOT → Semantics → Query/Mutation repositories → high-assurance mutations → MCP → Testing), following the sample's `GUIDE.md`. Linked from the site nav, `sitemap.xml`, `llms.txt`, and `llms-full.md`.

### Fixed
- **`samples/Foundgine.SupplyChain/Domain/Foundgine.SupplyChain.Domain.csproj` was missing the `Foundgine.Aot.Generator` analyzer reference.** The project declares `[FoundgineModel]`/`[FoundgineEntity]`/`[FoundgineField]`-attributed types but only referenced `Foundgine.Aot` as a plain `ProjectReference`, which does not transitively add `Foundgine.Aot.Generator` as an analyzer to `Domain`'s own compilation. The generator therefore never ran for `Domain`, so `Foundgine.Generated.GeneratedMetadata` (consumed by `Semantics/SupplyChainSemanticModel.cs` and, downstream, `Infrastructure` and `Tests`) was never emitted, and the sample failed to compile with `CS0234` on `Foundgine.Generated`. Added the missing `<ProjectReference Include="../../../src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all" />` entry, matching the pattern already used correctly in `tests/Foundgine.Aot.Tests` and `tests/Foundgine.E2E.Tests`.
- `docs-site/index.html` — the three "problem" callouts under the homepage hero (`Too many bespoke tools`, `Too much agent work`, `Execution rules get fragmented`) had no separator between the bold lead-in and the following sentence in the markup, so the two ran together as a single word (e.g. `toolsBusiness`) in contexts where the `.problem-grid strong`/`span` block-display CSS isn't applied. Added terminal punctuation and a space so the text reads correctly regardless of rendering context.

### Known verification gap
- The `Domain.csproj` fix above has not been verified with an actual `dotnet build`/`dotnet test` run in the environment that prepared this release (no .NET SDK available there). It should be verified in CI, or locally with `dotnet build samples/Foundgine.SupplyChain/Foundgine.SupplyChain.sln`, before this version is tagged and packages are published. No `src/` package code changed in this release, so the existing 0.5.0 restore/build/test evidence for the packages themselves still stands.

## [0.5.0]

### Changed
- **`Foundgine.Authorization` promoted to a real, packaged library.** The authorization recovery control plane — witness quorum, credential lifecycle, journal reconciliation, and failover — moved out of `samples/Foundgine.HighAssurance.Postgres/Authorization/` and `.../Execution/` into a new `src/Foundgine.Authorization/` project, under a single `Foundgine.Authorization` namespace, depending only on `Foundgine.Execution` and the BCL. The two files that hardcode the sample's `transferFunds` operation (`AuthorizationDecision.cs`, `AuthorizationExecutionBinding.cs`) and the four genuinely Postgres-specific files (`PostgresAuthorizationContextStore`, `PostgresAuthorizationRecoveryCoordinator`, `PostgresAuthorizationSecurityUnitOfWork`, `PostgresTransferFundsExecutor`) stayed in the sample.
- **`Foundgine.sln` fixed.** Removed a duplicate `ProjectConfigurationPlatforms` block that sat outside any `GlobalSection`, added the missing `Release|Any CPU` build configuration for the Banking/Postgres sample projects, and registered the new `Foundgine.Authorization` project.
- **Milestone-numbering scheme removed from public surfaces.** Internal tracking IDs previously embedded in doc comments, README section headers, and changelog entries carried no meaning outside the original development process and are now gone; section headers use plain descriptive titles instead.
- **Documentation index rewritten.** `docs/README.md` no longer links to files that don't exist in this repository. The same dead-link and stale-path cleanup was applied to `docs/ROADMAP.md`, `docs/SECURITY.md`, `docs/security/CHANGELOG.md`, `README.md`, `ai.seo.md`, and `llms-full.md`.

### Fixed
- Test files under `tests/Foundgine.Authorization.Tests` that reference the relocated `Foundgine.Authorization` types were missing the corresponding `using Foundgine.Authorization;` directive after the move; added.

## [0.4.0]

### Added
- **Authorization recovery control plane.** Adds authorization-recovery handling covering the failure and recovery paths of the authorization control plane: publication key lifecycle, rotation and retirement, promotion and commit atomicity, cross-instance commit and journal consensus/reconciliation, repair ordering and idempotency, and repair-proposer credential authentication, lifecycle, and replication. Full invariant-by-invariant detail lives in `docs/security/`; adversarial coverage lives in `tests/Foundgine.Authorization.Tests/`.
- **Authority-term replication & recovery certificates.** Authority terms are installed through cryptographically signed direct-successor certificates with a chained history digest, preventing forged, skipped, divergent, or replayed authority transitions during replication and recovery.
- **Authority-term certificate quorum / multi-witness validation.** Independent witness attestations over authority-term certificate digests, with strict-majority validation against configured witness identities and defenses against duplicate, unknown-witness, wrong-key, minority, and certificate-tamper conditions. The authoritative anchor remains the sole mutation authority; witness quorum is corroboration only.
- **Witness credential lifecycle, rotation & revocation security.** Lifecycle-managed witness credentials with monotonic credential generations, compare-and-swap rotation, terminal revocation, lifecycle-backed authentication, revocation-aware in-flight credential leases, and fail-closed handling for unknown, stale, and revoked credentials.
- **Witness credential lifecycle replication & crash recovery security.** A durable-safe witness credential lifecycle journal with monotonic revisions and chained SHA-256 digests, contiguous replication with idempotent duplicate handling and fail-closed gap/divergence detection, crash-recovery packages that replay complete lifecycle history without transporting credential secrets, and defenses against tamper, rollback, skipped-history, divergent-revision, and revoked-history conditions.
- **Projection pruning.** The planner includes a conservative projection-pruning rule that removes redundant duplicate fields without changing requested field order. Fields required by filters and ordering are tracked explicitly, and every accepted rewrite continues through semantic-equivalence and security-preservation proofs. The current semantic model intentionally does not remove unique requested fields, because output and working projections are not yet represented separately — that stronger dead-field optimization is reserved for a future projection-dependency milestone.
- **Join ordering / multi-relationship planning.** Adds conservative cardinality- and selectivity-aware traversal ordering metadata for sibling relationship plans. Logical child order remains unchanged; providers may use `TraversalOrder` for physical planning subject to semantic and security conformance.
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
