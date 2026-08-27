# NuGet Package Release Matrix

This matrix maps each packable Foundgine package to the release changes that are relevant to that package. The same information is embedded in each package's `PackageReleaseNotes` metadata so it is visible from NuGet package metadata.

## 1.1.1
- **Foundgine.Security.Authority** — 1.1.1: Renamed from `Foundgine.Authorization` to make the authority/recovery boundary explicit and keep distributed authorization control-plane concerns outside the Foundgine semantic execution core.
- **Other Foundgine packages** — 1.1.1: No runtime changes from this architectural separation.

## 1.1.0
- **Foundgine.GraphQL.HotChocolate** — 1.1.0: Keeps the GraphQL query adapter pure and independent of execution.
- **Foundgine.GraphQL.HotChocolate.Execution** — 1.1.0: Adds the secure `FoundgineHotChocolateQueryExecutor` and host-owned security execution-context integration.
- **Foundgine.GraphQL.HotChocolate.MutationExecution** — 1.1.1: Adds the secure `FoundgineHotChocolateMutationExecutor`, canonical semantic mutation conversion, host-owned security context enforcement, warrant validation, replay protection, and final mutation execution certification.
- **Foundgine.MCP** — 1.1.0: Converges on the shared `ISecurityExecutionContextProvider` contract while retaining the compatibility delegate path.
- **Foundgine.Semantics** — 1.1.0: Adds the shared host-owned security execution-context provider contract and helpers.
- **Other Foundgine packages** — 1.1.0: Versioned with the release line; no unrelated breaking API changes are introduced.

## 1.0.0
- **Foundgine.Planning** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.AI** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Metadata** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.InMemory** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.GraphQL.HotChocolate** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.MCP** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Execution** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.GraphQL.HotChocolate.Mutations** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Semantics** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Intent.Json** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Sql** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Abstractions** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Security.Authority** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine.Aot** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
- **Foundgine** — 1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.

## 0.5.0
- **Foundgine.Planning** — 0.5.0: Packaging/documentation cleanup only; no planner runtime capability change.
- **Foundgine.AI** — 0.5.0: Packaging/documentation cleanup only; no AI adapter runtime capability change. Provides Microsoft.Extensions.AI integration for Foundgine semantic execution.
- **Foundgine.Metadata** — 0.5.0: Packaging/documentation cleanup only; no metadata-model runtime capability change.
- **Foundgine.InMemory** — 0.5.0: Packaging/documentation cleanup only; no InMemory provider runtime capability change.
- **Foundgine.GraphQL.HotChocolate** — 0.5.0: Packaging/documentation cleanup only; no GraphQL adapter runtime capability change.
- **Foundgine.MCP** — 0.5.0: Packaging/documentation cleanup only; no MCP adapter runtime capability change.
- **Foundgine.Execution** — 0.5.0: Packaging/documentation cleanup only; no execution-boundary runtime capability change.
- **Foundgine.GraphQL.HotChocolate.Mutations** — 0.5.0: Packaging/documentation cleanup only; no mutation adapter runtime capability change.
- **Foundgine.Semantics** — 0.5.0: Packaging/documentation cleanup only; no semantic-model runtime capability change.
- **Foundgine.Intent.Json** — 0.5.0: Packaging/documentation cleanup only; no JSON intent adapter runtime capability change.
- **Foundgine.Sql** — 0.5.0: Packaging/documentation cleanup only; no SQL provider runtime capability change.
- **Foundgine.Abstractions** — 0.5.0: Packaging/documentation cleanup only; no abstraction-contract runtime capability change.
- **Foundgine.Security.Authority** — 0.5.0: New packaged authorization recovery control-plane library, extracted from the PostgreSQL sample. Provides provider-agnostic witness quorum, credential lifecycle, journal reconciliation, and failover primitives.
- **Foundgine.Aot** — 0.5.0: Packaging/documentation cleanup only; no AOT runtime capability change.
- **Foundgine** — 0.5.0: Packaging/documentation cleanup only; no core facade runtime capability change.

## Naming note
`Foundgine.Security.Authority` is the renamed authority/recovery package formerly published as `Foundgine.Authorization`. The 0.5.0 and 1.0.0 entries below describe the historical package under its former name.

## Package-specific history
### Foundgine.Planning
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no planner runtime capability change.
- 0.4.0: Added provider-aware rewrite cost estimation and deterministic rule selection, predicate pushdown, projection pruning, relationship traversal/join ordering, aggregate/cardinality-aware rewrites, and semantic-equivalence plus authorization-preservation proof gates. Added deterministic authorization-predicate canonicalization and plan fingerprints.

### Foundgine.AI
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no AI adapter runtime capability change. Provides Microsoft.Extensions.AI integration for Foundgine semantic execution.

### Foundgine.Metadata
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no metadata-model runtime capability change.
- 0.3.0: Metadata and semantic modeling became part of the validated semantic execution release surface. AOT metadata support builds on this package.

### Foundgine.InMemory
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no InMemory provider runtime capability change.
- 0.3.0: InMemory execution became part of the validated provider execution surface.

### Foundgine.GraphQL.HotChocolate
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no GraphQL adapter runtime capability change.
- 0.3.0: Hot Chocolate GraphQL adapter became part of the validated release surface for translating GraphQL selections into Foundgine semantic requests.

### Foundgine.MCP
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no MCP adapter runtime capability change.
- 0.3.0: MCP became part of the validated release surface, including mutation-safe execution contracts.

### Foundgine.Execution
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no execution-boundary runtime capability change.
- 0.3.0: Provider-independent execution planning, execution receipts, and plan-bound approval became part of the validated release surface.

### Foundgine.GraphQL.HotChocolate.Mutations
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no mutation adapter runtime capability change.
- 0.3.0: GraphQL mutation integration became part of the validated semantic execution surface.

### Foundgine.Semantics
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no semantic-model runtime capability change.
- 0.4.0: Authorization-predicate canonicalization and deterministic normalization fed deterministic plan fingerprints.
- 0.3.0: Semantic modeling, request resolution, authorization-aware query/mutation planning, relationship traversal, filtering, aggregation, and pagination were part of the validated release surface.

### Foundgine.Intent.Json
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no JSON intent adapter runtime capability change.
- 0.3.0: JSON became part of the validated adapter surface for provider-neutral semantic requests.

### Foundgine.Sql
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no SQL provider runtime capability change.
- 0.3.0: SQL execution and PostgreSQL query/mutation compilation became part of the validated execution surface.

### Foundgine.Abstractions
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no abstraction-contract runtime capability change.
- 0.3.0: Provider-independent contracts and identifiers formed the foundation of the validated semantic execution surface.

### Foundgine.Security.Authority
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: New packaged authorization recovery control-plane library, extracted from the PostgreSQL sample. Provides provider-agnostic witness quorum, credential lifecycle, journal reconciliation, and failover primitives.
- 0.4.0: The underlying authorization recovery/security implementation existed in the PostgreSQL reference sample; 0.5.0 is the first standalone NuGet package for the provider-agnostic portion.

### Foundgine.Aot
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no AOT runtime capability change.
- 0.3.0: AOT metadata generation became part of the validated release surface; this package bundles the required metadata runtime and generator assets.

### Foundgine
1.0.0: Semver stability declaration only; no runtime capability change since 0.5.0.
0.5.0: Packaging/documentation cleanup only; no core facade runtime capability change.
- 0.3.0: Foundgine's semantic execution architecture became a validated release surface covering intent, semantics, planning, authorization-aware execution, deterministic fingerprints, and provider integration.
