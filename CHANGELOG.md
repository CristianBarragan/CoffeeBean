## [1.1.7] - 2026-08-30

### Fixed
- **Removed a duplicate `SemanticIdentity` type that shadowed the real one in tests.** `Foundgine.Semantics` contained an unrelated, unused `record SemanticIdentity(FieldId FieldId, string Name)` that shared its name with the canonical `static class SemanticIdentity` in `Foundgine.Abstractions`. Because `Foundgine.Semantics.Tests` is nested under the `Foundgine.Semantics` namespace, unqualified `SemanticIdentity` references resolved to the enclosing-namespace record instead of the intended `using`-imported class, breaking `SemanticIdentityTests`. The dead record has been deleted.
- **Stopped benchmark/sample `Exe` projects from being packed and published as NuGet packages.** `Foundgine.sln` includes several non-library `OutputType=Exe` projects — including three unrelated projects all literally named `Database` plus `CoffeeBeanery.Database` and `CoffeeBeanery.LoadTest` — that had no `IsPackable` setting and were picked up by `dotnet pack Foundgine.sln` during release, producing spurious `.nupkg` files with generic/colliding package IDs (causing the `1.1.6` release publish step to fail with `403 Forbidden` on the ownerless `Database` package ID, after `CoffeeBeanery.Database`/`CoffeeBeanery.LoadTest` had already leaked out as accidental publishes in an earlier release). `Directory.Build.props` now defaults `IsPackable` to `false` repo-wide; only the 17 intentional library projects under `src/` opt back in explicitly alongside their `PackageId`.

## [1.1.6] - 2026-08-30

### Added
- **Provider-backed semantic retrieval test coverage in `samples/Foundgine.SupplyChain.Semantic`.** New `Tests/Retrieval/` suite exercises every `RetrievalStrategy` supported by `Foundgine.Sql.Retrieval.PostgresRetrievalCandidateSource` against the sample's own Supplier/Product domain:
  - `SupplyChainRetrievalCapabilityTests` — provider-wiring unit tests requiring no live database: the reserved `Vector` strategy always throws `NotSupportedException`; `Relational` is confirmed as a documented no-op; `Fuzzy`, `FullText`, `Search`, and `GraphSimilarity` each reject when their opt-in flag is off; `GraphSimilarity` request validation (missing `Relationship`/`ReferenceIdentity`) fails fast even when enabled; constructor null-argument guards.
  - `SupplyChainFuzzyAndFullTextRetrievalTests` — live PostgreSQL integration tests for the two default-enabled providers: `pg_trgm` fuzzy matching on `Supplier.Name` (including limit and no-match behavior) and native `tsvector` full text search on `Product.Name`, seeded against a schema matching the sample's generated storage names.
  - `SupplyChainSearchRetrievalTests` — live integration tests for the optional `pg_search`/BM25 provider, gated behind an explicit `FOUNDGINE_POSTGRES_PGSEARCH=1` opt-in on top of the database connection since the extension isn't present on a vanilla PostgreSQL image.
  - `SupplyChainGraphSimilarityRetrievalTests` — live integration tests for the optional Apache AGE graph-similarity provider, gated behind `FOUNDGINE_POSTGRES_AGE=1`, seeding a small Cypher graph over the `Supplier.purchaseOrders` relationship to demonstrate neighbor-similarity candidate retrieval.
  - `PostgresRetrievalFactAttribute` (`PostgresRetrievalFactAttribute`/`PgSearchFactAttribute`/`ApacheAgeFactAttribute`) — connection- and extension-gated `[Fact]` variants, mirroring the existing `Foundgine.Security.Authority.Tests.PostgresFactAttribute` pattern, so the new tests skip cleanly without a configured database instead of failing CI.
  - `Tests/Foundgine.SupplyChain.Semantic.Tests.csproj` now references `Foundgine.Sql` and `Npgsql` to support the new suite.
- **Scenario 6 — Approximate & provider-backed retrieval** added to the advanced semantics sample page (`docs-site/samples/semantic/index.html`), documenting the provider-neutral `RetrievalStrategy` contract and how the PostgreSQL provider backs `Fuzzy` (pg_trgm), `FullText` (native tsvector), `Search` (optional pg_search/BM25), and `GraphSimilarity` (optional Apache AGE) while intentionally reserving `Vector` for a future `pgvector` provider. The "Provider lowering" architecture layer summary was updated to reference the same boundary.

## Step 36 — MCP Capability Discovery → Intent → Execution

- Added `FoundgineMcpAgentClient` for capability discovery and provider-neutral dynamic query execution.
- Added complete discovery → intent → execution workflow support.
- Added JSON/SSE MCP response handling and JSON-RPC error handling.
- Added client conformance tests proving discovered capabilities drive dynamic intent construction without client-supplied security authority.
- Added `SEMANTIC-MCP-CAPABILITY-DISCOVERY-STEP36.md`.


## Step 34 — Planner Algebra / Optimization

- Added explicit optimization preservation proof covering semantic meaning, security obligations, and authorization binding.
- Kept provider cost estimates advisory for candidate selection rather than correctness evidence.
- Added regression tests for optimization-proof dimensions.
- Added `SEMANTIC-PLANNER-OPTIMIZATION-STEP34.md`.
## Step 30 — Semantic Operation Graph & Planner Algebra

- Added immutable `SemanticOperationGraph` as an explicit provider-neutral intermediate representation over canonical Semantic IR.
- Added graph validation and pure round-trip conversion back to `SemanticOperation`.
- Added initial planner algebra for predicate composition and deterministic field normalization.
- Added regression tests for topology, immutability/snapshot semantics, round-tripping, and non-mutating algebra.


## Documentation — Semantic execution lifecycle

- Formalized the Semantic Operation Graph → Authorization → Plan Binding → Execution lifecycle across the architecture, authorization, runtime and planning documentation.
- Documented authorization provenance through `SemanticPlanAuthorizationBinding`, `ExecutionIR`, provider-plan binding and the final security gate.
- Added the same lifecycle to the website architecture and execution walkthrough.

# Step 28 — Execution / Provider Boundary

## Step 29 — Execution-Time Authorization Revalidation

- Added the final execution-time authorization revalidation boundary.
- Added `IExecutionAuthorizationRevalidator` and the default semantic implementation.
- Added optional current authority resolution to `FoundgineOptions`.
- Extended semantic authorization evidence with optional authority version/fingerprint binding.
- Revalidation occurs after provider-plan cache lookup and immediately before provider execution.
- Revoked or superseded authority fails closed.


- Bound ExecutionIR and provider plans to semantic authorization provenance.
- Added fail-closed provider boundary validation.
- Added execution/provider boundary documentation.

## Step 21 — Semantic Contract Runtime Boundary

## Step 27 — Semantic Contract Plan Optimization Binding

- Added authorization-binding preservation proof for plan rewrites.
- Optimizer rejects rewrites that add, remove, or change contract/authorization provenance.
- Optimization results now expose the final authorization-binding proof.
- Added Step 27 regression documentation.


## Step 26 — Semantic Contract Plan Authorization Binding

- Bound authorized semantic plans to the immutable semantic contract fingerprint and authorization fingerprint.
- Added contract-aware planner overload accepting `SemanticAuthorizationResult`.
- Preserved authorization binding through plan rewrites.
- Required matching authorization evidence at executable plan boundary.
- Added Step 26 regression coverage and architecture documentation.


## Step 24 — Semantic Contract Authorization Boundary

- Added contract-aware authorization using `SemanticContractSnapshot`.
- Added pre-policy validation of semantic operation identity and relationship integrity.
- Updated `FoundgineEngine` runtime authorization to use the immutable contract snapshot.
- Added authorization boundary regression coverage and documentation.



## Step 22 — Semantic Contract Runtime Consumer


- Migrated `SemanticRequestResolver` runtime consumption to `SemanticContractSnapshot`.
- Added snapshot-aware semantic graph and filter validation.
- Updated `FoundgineEngine` to consume the startup singleton snapshot for request resolution.
- Retained model overloads/constructor as compatibility bridges.
- Added runtime-boundary regression coverage and documentation.

- Added `ISemanticContractProvider` as the runtime dependency port for trusted semantic state.
- Added `SemanticContractProvider` for immutable singleton contract delivery.
- `AddFoundgine(...)` now freezes the configured semantic model and creates one application-lifetime `SemanticContractSnapshot`.
- Registered the snapshot and provider through dependency injection.
- Added documentation for the construction-to-runtime dependency boundary.

# Changelog

## Step 23 — Semantic Contract Planning Boundary

- Added contract-aware `IPlanner.Plan(SemanticContractSnapshot, SemanticOperation)`.
- Added runtime validation that canonical semantic IR belongs to the trusted frozen contract.
- `FoundgineEngine` now supplies the immutable semantic snapshot to planning.
- Added regression coverage for unknown entities, fields, and relationship target mismatches.


## Step 20 — Semantic Contract Snapshot

- Added `SemanticContractSnapshot` as the explicit immutable runtime representation of a frozen semantic contract.
- Added `SemanticModel.CreateSnapshot()` with a fail-closed frozen-model requirement.
- Preserved the canonical contract fingerprint across snapshot creation.
- Added defensive copying for nested semantic collections and traversal paths.
- Added documentation for the construction-to-trusted-runtime lifecycle boundary.

## Step 19 — Semantic Contract Immutability & Freeze

- Added an explicit `SemanticModel.Freeze()` lifecycle boundary and `EnsureFrozen()` guard.
- Preserved `ContractFingerprint` across freezing and made freezing idempotent.
- Added defensive deep copies/read-only wrappers for semantic entity, field, relationship, alias, constraint, and traversal collections.
- Added regression coverage proving post-build lifecycle state, fingerprint preservation, idempotence, and nested collection immutability.
- Added `SEMANTIC-CONTRACT-FREEZE-STEP19.md`.


## Step 17 — Semantic Nullability Contract

- Preserve nullable-reference metadata in typed semantic fields.
- Include field nullability in the canonical contract identity.
- Add regression coverage proving `string` and `string?` produce different semantic contracts.
## Step 16 — Semantic Version / Contract Fingerprint Unification

- Made `SemanticVersionSet.SemanticModelVersion` a direct projection of `SemanticModel.ContractFingerprint`.
- Removed the second model-version hashing algorithm to prevent divergent notions of semantic-model identity.
- Cache the immutable model contract fingerprint at construction time.
- Expanded fingerprint canonicalization to include field nullability and traversal targets.
- Added regression coverage for version/fingerprint equivalence and alias-driven contract changes.


## Step 15 — Semantic Contract Fingerprint

- Added canonical `SemanticModel.ContractFingerprint`.
- Fingerprinting is deterministic across declaration order and independent CLR type identity.
- Added regression coverage for stable, changing, and format-valid fingerprints.
- Added `IDENTITY-FINGERPRINT-STEP15.md`.


## Identity Step 14 — Relationship Identity Consistency

- Hardened global relationship identity validation so repeated canonical relationship declarations must agree on target entity and cardinality.
- Added regression coverage for target and cardinality conflicts.
- Added `IDENTITY-CONSISTENCY-STEP14.md` documenting the invariant that stable relationship identity implies a stable semantic contract.


## Step 13 — Relationship identity collision hardening

- Added global relationship identity collision detection during semantic model composition.
- Composition now fails closed when different semantic relationships share a `RelationshipId`.
- Added regression coverage for direct and independently imported module collisions.
- Preserved legacy explicit relationship-ID compatibility while making deterministic typed declarations the preferred path.


## Identity architecture hardening

- promoted deterministic `RelationshipId.Create(entity, relationship)` through typed relationship builder overloads;
- added semantic graph validation modes: Strict, Loose, Federated, and Exploratory;
- added graph annotations/provenance, expected cardinality, nullable-path metadata, and semantic constraints;
- added field constraints for range, pattern, temporal semantics, currency, and country code;
- extended entity resolution with semantic-identity, traversal, composite-key, temporal, and non-authoritative fuzzy-name paths;
- marked the legacy untyped semantic entity builder as obsolete and hidden from normal IntelliSense while retaining compatibility.

# Changelog

## Step 19 — Semantic Contract Immutability & Freeze

- Added an explicit `SemanticModel.Freeze()` lifecycle boundary and `EnsureFrozen()` guard.
- Preserved `ContractFingerprint` across freezing and made freezing idempotent.
- Added defensive deep copies/read-only wrappers for semantic entity, field, relationship, alias, constraint, and traversal collections.
- Added regression coverage proving post-build lifecycle state, fingerprint preservation, idempotence, and nested collection immutability.
- Added `SEMANTIC-CONTRACT-FREEZE-STEP19.md`.


## [Unreleased] — Identity Regression / Step 12

### Added
- **Identity determinism regression gate.** Added `IdentityDeterminismTests` covering declaration reordering, independent compilation, module composition, alias stability, full identity JSON round-trips, reserved-zero enforcement, and duplicate explicit entity IDs.
- Added `IDENTITY-REGRESSION-STEP12.md` documenting the identity contract and the criteria required before freezing it.

### Fixed
- The AOT generator now distinguishes an omitted explicit identity from `Id = 0`. Explicit zero is rejected instead of being silently treated as an automatically allocated identity.
- Reserved-zero validation is now applied consistently to generated model, connection, and authorization identities in addition to entities, fields, columns, and relationships.


## [1.1.5] - 2026-08-29

### Added
- **Metadata-backed semantic discovery.** Added `IMetadataCatalog` as the model-wide structural metadata contract, plus `SemanticModel.Discover(...)` and `SemanticModelBuilder.FromMetadata(...)`. Structural entities, fields, identities and direct relationships can now be discovered from `Foundgine.Metadata` without application semantic enumeration. Added CLR type and collection-shape information to structural metadata so semantic discovery can preserve model type and relationship cardinality.
- **`FoundgineOptions.UseMetadata()` and semantic/authorization configuration hooks**, making metadata-backed model discovery the preferred application setup path. Added name-based logical traversal configuration so applications no longer depend on generated relationship IDs.
- **Semantic mutation intent builder** (`Foundgine.Semantics/Mutation/SemanticMutationIntentBuilder.cs`) and a new fluent `QueryBuilder`/`MutationBuilder` surface in `Foundgine`/`FoundgineServiceCollectionExtensions.cs`.
- **Metadata structural contract.** Added compile-time validation for Foundgine relationship metadata: relationship targets must be discovered Foundgine entities; navigation property targets must match the declared relationship target; foreign-key and principal-key properties must exist, be scalar properties, and be unambiguous; foreign-key and principal-key CLR types must match. Invalid relationship metadata now reports deterministic `FGMETA001`–`FGMETA007` diagnostics and does not emit the generated registry.
- **Domain CLR metadata producer boundary.** Extended the AOT metadata generator to discover `[FoundgineEntity]` declarations directly on C# record types, making the SupplyChain domain declarations the structural source observed by the AOT producer, while `SupplyChainMetadataProducer` remains the `IMetadataCatalog` seam consumed by semantics.
- **New `samples/Foundgine.SupplyChain.Simple` sample** — the canonical Supply Chain sample collapsed from 6 projects into 1 (folders instead of project boundaries), demonstrating that the AOT generator and layer separation don't require separate assemblies.
- Traversal paths are now included in semantic model version hashing (`SemanticVersion.cs`), so a changed logical traversal path changes the model's version identity.
- Added tests covering metadata discovery, logical traversal enrichment, multi-hop traversal configuration, capability-contract authorization-policy propagation, and semantic mutation intent building.

### Changed
- Moved the canonical Supply Chain sample's semantic enrichment into `Application/SupplyChainSemanticConfiguration.cs` and removed the `Foundgine.SupplyChain.Semantics` project from the canonical sample; the discovered/enriched semantic model is now registered through the sample's infrastructure composition root.
- Removed the SupplyChain semantic sample's parallel structural model, now that domain declarations are the structural source.

### Fixed
- Fixed capability-contract traversal discovery to pass the active authorization policy through the capability builder.

## [1.1.4] - 2026-08-28

### Fixed
- **`Foundgine.SupplyChain.Semantic` had one last CI-only build error.** `Api/Mcp/Program.cs`'s `policy_probe` tool built a `result` from a `switch` expression whose arms return either `AuthorizationPredicate` (from `GetPredicate`), `AuthorizationDecision` (from `GetEntityAccess`/`GetFieldAccess`/`GetRelationshipAccess`), or `null` — two unrelated `sealed record` types with no common type but `object`, so `var result = attack switch { ... }` couldn't infer a natural type (`CS8506`). The very next statement already pattern-matches `result` back apart by concrete type (`AuthorizationPredicate predicate => ...`, `AuthorizationDecision decision => ...`), so the fix is to declare `result` as `object?` explicitly instead of `var` — each arm then target-converts to `object?` individually instead of needing to unify with the others, matching the pattern already used one line below for `object body = result switch { ... }`.
- **`Foundgine.SupplyChain.Semantic` still failed on CI (Linux, `dotnet` 10 SDK) with 16 more errors, all stemming from a single bad path plus one array-typing bug.** (1) `Api/Mcp/Foundgine.SupplyChain.Semantic.Mcp.Api.csproj` referenced its parent project as `../../../Foundgine.SupplyChain.Semantic.csproj` — three levels up from `Api/Mcp/`, which lands in `samples/` instead of `samples/Foundgine.SupplyChain.Semantic/` (`MSB9008`, then everything from `Program.cs` failed to resolve — `CS0246` on `Foundgine`, `SemanticModel`, `SupplyChainRole`, `SupplyChainAuthorizationPolicy`, `ClaimsValidationResult`). Corrected to `../../Foundgine.SupplyChain.Semantic.csproj` (two levels up), matching where the project file actually is. (2) `McpClient/Program.cs`'s `cases` array (`CS0826`, no best type for implicitly-typed array) mixed several anonymous types with different property sets in the same `var cases = new[] { (label, tool, new { ... }), ... }` literal — each distinct `new { ... }` shape is a distinct anonymous type, so implicit array-type inference has no common type to pick. Declared the array's element type explicitly as `(string, string, object)[]`, which only needs each anonymous type to convert to `object` rather than needing them to unify with each other.
- **`Foundgine.SupplyChain.Semantic` still failed to build after the previous fix, with a further 8 errors.** (1) `SupplyChainAuthorizationPolicy.CanAccessEntity` referenced `SupplyChainSemanticModel.SupplierCertification` (`CS0117`), but the entity's `EntityId` field is named `SupplyChainSemanticModel.Certification` (the domain *type* is `SupplierCertification`; the semantic-model *entity id* is `Certification`) — corrected the reference. (2) `Semantics/Generated/SupplyChainGeneratedSemanticModel.cs` failed with `CS0229` ambiguous-reference errors on `PurchaseOrder`, `PurchaseOrderLine`, and `Shipment`, because it combined `using Foundgine.SupplyChain.Semantic.Domain;` (bringing in the domain *types* `PurchaseOrder`, `PurchaseOrderLine`, `Shipment`) with `using static ...SupplyChainSemanticModel;` (bringing in the same-named `EntityId` *fields* at the same scope) — both are ordinary `using` imports at file scope, so the compiler can't prefer one over the other. Replaced the `using static` with a normal `using` plus explicit `SupplyChainSemanticModel.` qualification on every `EntityId` reference, matching the pattern the (unambiguous) manually authored model already uses.
- **`Foundgine.SupplyChain.Semantic` failed to build.** Two issues surfaced together: (1) the sample's main project had no `Compile Remove` for `Api/Mcp/**/*.cs` or `McpClient/**/*.cs`, so the SDK's default recursive glob pulled those two separate sub-projects' `Program.cs` files (and their `ModelContextProtocol`/ASP.NET Core-only types) straight into the main assembly, causing `CS8802` (multiple top-level-statement files) and cascading `CS0246`/`CS0234` missing-type errors. Both subdirectories are now excluded from the main project's compile items, matching the existing `Tests/**/*.cs` exclusion. (2) `SupplyChainAuthorizationPolicy`'s 3-argument `GetEntityAccess(entityId, operation, name)` override didn't compile (`CS0115`) because its base class, `AllowAllSemanticAuthorizationPolicy`, only implemented the 2-argument overload as a class member — the 3-argument overload existed solely as a C# default interface method on `ISemanticAuthorizationPolicy`, which a derived class cannot `override`. `AllowAllSemanticAuthorizationPolicy` now declares its own `virtual` 3-argument `GetEntityAccess`, delegating to the 2-argument overload exactly as the interface default did, so derived policies can override it as intended.

### Added
- **Client-supplied claims validation in `samples/Foundgine.SupplyChain.Semantic`.** The SupplyChain MCP tools (`read_entity`, `write_entity`, `policy_probe`) now accept an optional, untrusted `claims` dictionary sent by the MCP caller itself, alongside the existing (unchanged) `actor`/`token` authentication. New `Authorization/ClientClaims.cs` adds:
  - `ClientClaimsValidator.Validate(...)`, a fail-closed validator that never has access to the caller's authenticated identity, so it cannot cross-check or "average" a claim against reality into something more permissive.
  - A hard, whole-request rejection for any claim that tries to assert identity or privilege directly (`role`, `tenant`, `tenantId`, `actor`, `isAdmin`, `admin`, `permissions`, `capabilities`, `scopes`) — presence alone fails the call closed, even when the asserted value happens to match the caller's real identity.
  - Per-key format validation for recognized narrowing/evidence claims — `scope` (`read-only`/`full`), `warehouse` (positive integer), `max_rows` (1–10,000), `reason` (8–240 chars), `change_ticket` (`CHG-####`), `not_after` (ISO-8601) — with malformed values rejected individually rather than failing the whole request.
  - Fail-open handling of unrecognized keys: dropped individually and reported back, without blocking the rest of the call.
  - Cross-field staleness checking: evidence (`reason`, `change_ticket`) paired with an already-expired `not_after` is rejected as stale.
- **`SupplyChainAuthorizationPolicy` now accepts a validated claim set** via a new `(tenantId, role, ClaimsValidationResult)` constructor overload (the original two-argument constructor is unchanged and delegates to it with an empty claim set). Only the *validated* `Accepted` claims are ever visible to the policy — there is no code path from a rejected or malformed claim into an authorization decision. Three claims are wired in, and every one can only narrow what the role already allows, never widen it:
  - `scope=read-only` — lets a caller self-restrict its own write access for a single call.
  - `warehouse=<id>` — ANDs an additional resource predicate onto the existing tenant predicate for `Warehouse`/`InventoryLot` reads; combined with `AND`, never `OR`, so it can only shrink the result set.
  - `reason` + `change_ticket` — required, in addition to the existing manager-only role check, before the high-assurance `inventory.reconcile` named operation is allowed.
- **9 new MCP adversarial/legitimate-use cases** in `McpClient/Program.cs`: role-injection, tenant-injection, missing/malformed/expired reconcile evidence (attacks), plus self-imposed read-only scope, warehouse scoping, unknown-claim-key noise, and valid reconcile evidence (legitimate, self-narrowing uses that the policy must honor rather than silently ignore).
- **New unit test coverage**: `ClientClaimsValidatorTests` (identity-spoofing rejection per reserved key, value-match-doesn't-matter, unrecognized-key drop, malformed-value rejection per recognized key, expired-evidence cross-field rejection, well-formed-evidence acceptance) and policy-level tests confirming self-narrowing scope/warehouse claims are honored and that evidence claims can never substitute for the role check. `AuthorizationMcpPenetrationTests` gained a `Claims_attack_matrix_defines_the_required_claim_validation_boundaries` test listing the 5 claim attacks and 4 legitimate narrowing uses as a single source of truth.
- **New `samples/Foundgine.SupplyChain.Semantic/GUIDE.md`** — a detailed guide covering the sample layout, the identity-vs-claims distinction, every claims validation rule, how the policy consumes accepted claims, the full MCP tool surface, the adversarial client's case list, and a complete attack/legitimate-use matrix. `README.md` and `Api/README.md` now link to it and summarize the claims feature inline.

### Changed
- `Api/Mcp/Program.cs` responses from `read_entity`, `write_entity`, and `policy_probe` now include `acceptedClaims`/`rejectedClaims` diagnostics alongside the existing result payload, so the adversarial client (and anyone exercising the tools directly) can see exactly which claims were honored and why any others weren't.

### Verification
- Static structural review (brace/paren balance, type/signature consistency against the existing codebase) was performed on every edited file.
- ZIP packaging integrity was verified after building the release contents.
- The .NET SDK is unavailable in the packaging environment, so `dotnet build`/`dotnet test` could not be run here; CI remains the authoritative runtime verification for this release.

## [1.1.3] - 2026-08-28

### Added
- **Strongly typed manual relationship API.** The manual `SemanticModelBuilder.Relationship<TFrom, TTo>(...)` surface now makes both sides of a relationship explicit domain-model generics (`<fromEntity, toModel>`) instead of an object/entity-abstraction pair, so each lambda's parameter is unambiguously typed to its own model (`product => product.Id`, `transaction => transaction.ProductId`) and cannot accidentally reference the other side's properties.
- **Mixed manual + generated semantic authoring in `samples/Foundgine.SupplyChain.Semantic`.** `SupplyChainSemanticModel.Build()` now imports a generated semantic artifact (`Semantics/Generated/`) via `SemanticModelBuilder.Import(...)` alongside manually authored entities and relationships, so both authoring paths converge on one immutable `SemanticModel`.
- **Authorization annotations and policies.** New `Authorization/PolicyAnnotations.cs` (`[SemanticEntity]`, `[SemanticField]`, `[SemanticPolicy("...")]`) describes semantic source metadata on the domain model; the authoritative runtime decision lives separately in the new `Authorization/SupplyChainAuthorizationPolicy.cs`, which demonstrates every authorization boundary Foundgine exposes: entity access, field access, relationship access, conditional (tenant) predicates, write access, and named-operation refinement (`inventory.reconcile`, manager-only).
- **SupplyChain MCP authorization lab.** New `Api/Mcp/Foundgine.SupplyChain.Semantic.Mcp.Api.csproj` hosts `describe_capabilities`, `read_entity`, `read_relationship`, `write_entity`, and `policy_probe` tools over a stateless MCP server, backed by a small fixed set of demo actor/token/role/tenant identities so tenant and role are always server-derived rather than caller-asserted.
- **Run-5-style adversarial MCP client.** New `McpClient/Foundgine.SupplyChain.Semantic.Mcp.Client.csproj` sends untrusted `tools/call` requests attempting capability-discovery abuse, cross-tenant reads, sensitive-field access, relationship escalation, write escalation, named-operation escalation, unauthorized writes, wrong-token/unknown-actor authentication probes, and an authorized-write positive control.
- **New `Tests/AuthorizationPolicyTests.cs` and `Tests/Mcp/AuthorizationMcpPenetrationTests.cs`** covering the policy's entity/field/relationship/conditional/write/named-operation distinctions and defining the MCP attack matrix.
- Root and Samples website pages updated to document the manual `<fromEntity, toModel>` relationship API and the mixed manual/generated + authorization + adversarial-MCP SupplyChain showcase.

### Verification
- ZIP integrity was verified after packaging; solution project/configuration entries were checked for structural consistency and the website sections were confirmed present.
- The .NET SDK was unavailable in the packaging environment, so `dotnet build`/`dotnet test` could not be run.


## [1.1.2] - 2026-08-28

### Fixed
- **Fixed `Foundgine.SupplyChain.Semantic` test compilation.** The sample application project sits above the `Tests/` directory, so the .NET SDK's default recursive compile glob was incorrectly compiling the xUnit test source files as part of the application project. That caused `CS0246` errors for `Xunit`, `Fact`, and `FactAttribute` when building the Semantic sample solution. The application project now explicitly excludes `Tests/**/*.cs`; the dedicated `Foundgine.SupplyChain.Semantic.Tests` project remains responsible for compiling and running the tests.
- **Aligned the Semantic sample test runner dependency with the main SupplyChain test project**, using `xunit.runner.visualstudio` 3.1.5.
- **Release version bumped to `1.1.2`** in `Directory.Build.props`.

### Changed
- **Supply Chain sample structure remains aligned across `Foundgine.SupplyChain`, `Foundgine.SupplyChain.Semantic`, and `Foundgine.SupplyChain.PenTest`.** The Semantic sample continues to use the same physical `Api/`, `Application/`, `Domain/`, `Infrastructure/`, `Semantics/`, and `Tests/` boundaries, with test compilation isolated to its test project.
- **Website navigation/header fixes carried forward into the release:** the AI Agents self-link is correctly active and uses the direct page path, the global navigation includes `Samples`, the Semantic/PenTest samples are discoverable from the Samples page, and the responsive mobile hamburger menu has working open/close, Escape, link-selection, and desktop-breakpoint behavior.
- **CI retains independent sample gates** for `Foundgine.SupplyChain` and `Foundgine.SupplyChain.Semantic`, so failures in either sample are surfaced independently from the main repository test job.

### Verification
- Static project/solution and workflow validation was performed for the release contents.
- An actual `dotnet build`/`dotnet test` run could not be performed in the packaging environment because the .NET SDK is not installed there; CI should provide the authoritative runtime verification.


### Build and sample fixes

- Fixed the Semantic sample seed data to construct `BusinessUnitId` explicitly for strongly typed IDs.
- Fixed PenTest API project references to the sibling `Foundgine.SupplyChain` sample after the aligned `Api/` layout.
- Verified all sample project references resolve from their new physical locations.


## [1.1.1] - 2026-08-27

### Architecture
- **Separated authorization authority/recovery from the Foundgine core.** The former `Foundgine.Authorization` package/project is now `Foundgine.Security.Authority`. The implementation remains provider-agnostic, but its purpose is explicit: authority recovery, witness quorum, credential lifecycle, journal reconciliation, promotion, failover, and recovery evidence. Foundgine core consumes validated security execution context and does not own the authority control plane. This is a package/namespace rename; consumers using `Foundgine.Authorization` must update their project reference and `using` directives.

### Added
- **New `samples/Foundgine.SupplyChain.PenTest` sample** — a dedicated dual-transport (GraphQL + MCP) penetration-test harness for the SupplyChain sample, separate from the getting-started `Foundgine.SupplyChain` sample so pentest infrastructure doesn't leak into the tutorial path. Includes `Graph.Api` and `Mcp.Api` hosts, a `docker-compose.yml` with an isolated seeded Postgres instance (port 4431), a `Tests` project (`GraphPenetrationTests`, `McpPenetrationTests`, `HostConfigurationTests`, `JsonEscapingRegressionTests`, plus `McpJsonRpcClient`/`PenTestConnectionString`/`SupplyChainPenTestFactAttribute` test infrastructure), and `README.md`/`GUIDE.md`/`TUTORIAL.md` docs. Runnable locally via `scripts/run-supplychain-pentest.ps1`.
- **New deterministic penetration suite under `tests/Foundgine.Security.Tests/Penetration/`** — 12 test files (~59 tests) covering cache/predicate-model attacks, cryptographic/identity attacks, graph/resource DoS, intent-parser hardening, JSON/MCP transport boundary abuse, mutation-authorization bypass, plan-integrity tampering, resource exhaustion, security-proof rails, semantic-boundary escapes, transport/secret leakage, and warrant trust-boundary attacks. This is the deterministic, CI-runnable counterpart to the `SEC-01`–`SEC-36` catalogue in `security/pentest/ATTACK-MATRIX.md`.
- **Warrant trust-boundary hardening**: `SecurityWarrantExecutionTrust` enforces full root-to-leaf delegation-chain verification (every signature re-verified, every delegation edge structurally validated, each delegating issuer explicitly trusted) instead of trusting a supplied chain at face value; `FileSecurityWarrantReplayStore` adds durable, lock-protected, cross-process replay protection for single-filesystem deployments (cloud deployments should still prefer a shared store). Backed by new `SecurityWarrantGuardRailsPenetrationTests` and `SecurityAuthorityPartitionRailsTests` covering issuer-trust bypass, delegation-chain forgery, replay races, cache-partition collisions, and capability/canonicalization confusion (SEC-25 through SEC-36).
- **`FieldId`** (`Foundgine.Abstractions`) — a `ushort`-backed value type with a custom `JsonConverter` so it can serialize both as an ordinary JSON value and as a dictionary key (`IReadOnlyDictionary<FieldId, object?>`), which `System.Text.Json`'s default converter can't do for a struct without explicit property-name read/write support. Covered by new `FieldIdJsonSerializationTests`.
- **New `security/pentest/` documentation**: `README.md` (attack-family overview and live-tool usage for Nmap/ZAP/Burp/Nessus/Metasploit), `ATTACK-MATRIX.md` (the SEC-01–SEC-36 status table), `PENTEST-STATUS.md` (why this matters, what's implemented vs. requires a live environment, and live-run evidence), and `CI-INTEGRATION.md`, plus `run-all.ps1`, `run-nmap.ps1`, `run-zap-graphql.ps1`, `run-zap-mcp.ps1`, and `mcp-protocol-fuzz.ps1` for live tooling.
- **CI**: `security.yml` gained `dependency-audit` (NuGet vulnerability audit across transitive packages, plus a deprecated-package report), `secret-scan` (gitleaks), `sbom` (CycloneDX SBOM generation), and `zap-baseline` (scheduled/manual dynamic OWASP ZAP scan combined with MCP protocol fuzzing against the SupplyChain sample) jobs. Added `.github/dependabot.yml`.

### Security
- **`samples/Foundgine.SupplyChain/Application/Authorization.cs` — closed an authentication bypass and an identity-spoofing gap in the sample's demo authorizer.** Previously `Demand` trusted any caller-supplied `actor` string with no proof of identity at all, and a `"customerN"` actor-naming pattern let a caller claim any customer's identity by encoding the ID into their own actor string. Added a real (demo) token-based `Authenticate` step with constant-time comparison (avoids leaking token length/prefix via timing), a fixed server-side actor→customer mapping instead of the spoofable naming pattern, and made the ownership check apply to *every* actor for *every* customer-scoped capability (it previously only checked `"alice"`); only `"admin"` may act across customers. `SupplyChainApplication` and the MCP/GraphQL hosts were updated to thread the token through every call, including the previously-unauthenticated `describe_capabilities`.

### Fixed
- **`samples/Foundgine.SupplyChain.PenTest/Graph.Api/Program.cs` — GraphQL query fields silently renamed by Hot Chocolate's `Get`-prefix stripping.** Hot Chocolate strips the `Get` prefix from resolver method names by convention (`GetMyOrders` → schema field `myOrders`), which broke `GraphPenetrationTests.Customer_cannot_read_another_customers_order_scope_idor` (it queried `getMyOrders`, which didn't exist) and silently affected `GetOrder`, `GetShipment`, `GetProduct`, and `GetInventory` the same way. Added explicit `[GraphQLName("getX")]` attributes to all five resolvers so the GraphQL field names stay aligned with the MCP tool names (`get_order`, `get_my_orders`, etc.) the API is deliberately mirrored on.
- **`samples/Foundgine.SupplyChain.PenTest/Tests/GraphPenetrationTests.cs` and `McpPenetrationTests.cs` — flaky `Injection_payload_in_tracking_number_is_stored_as_literal_text_not_executed` from cross-test data collision.** Both test classes hardcoded the identical literal tracking number (`"TRK-1'; DROP TABLE shipments; --"`) against the same shared Postgres test database, and `shipments.tracking_number` has a unique constraint. Whichever test class ran first inserted the row; the other collided on `shipments_tracking_number_key` and failed with a genuine Postgres duplicate-key error, misreported as "injection payload was rejected." The tracking number is now randomized per test run (`$"TRK-{Guid.NewGuid()}'; DROP TABLE shipments; --"`), matching the existing `idempotencyKey` randomization pattern already used elsewhere in both files, while preserving the injection shape of the payload.

### Changed
- **CI pipeline reorganized: all security/penetration jobs now live in `security.yml` instead of being split across `build.yml` and `security.yml`.** Moved `security-penetration` (Authorization/TransferFunds), `security-adversarial` (hostile model corpus + black-box adversarial engine), and `security-semantic-penetration` (Foundgine semantic-boundary suite) out of `build.yml` into `security.yml`. Removed `build.yml`'s `security-penetration-supplychain` job entirely, since it duplicated the SupplyChain GraphQL+MCP coverage already split into `security.yml`'s `security-penetration-graph` and `security-penetration-mcp` jobs. `build.yml`'s `publish-nuget` job no longer lists the moved jobs in `needs:` (GitHub Actions `needs:` cannot reference jobs in a different workflow file); the security jobs should instead be configured as required status checks in the repository's branch/tag protection rules.
- Renamed ambiguous `name` storage columns to entity-qualified names (`product_name`, `supplier_name`, `category_name`) in the SupplyChain sample's storage models.
- Various dependency bumps across sample/test/tooling projects (Npgsql, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, Microsoft.Extensions.DependencyInjection*, Microsoft.Data.Sqlite, HotChocolate.Language, Microsoft.CodeAnalysis.*, and several GitHub Actions versions) via Dependabot.

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
- **`VersionPrefix` bumped `0.5.0` → `1.0.0` in `Directory.Build.props`.** This is a semver-stability declaration, not a runtime capability change: no `src/` package's implementation changed in this release. It marks the public API surface documented in the historical 0.5.0 release notes (carried forward unchanged) as the first release under a `1.x` stability commitment.

### Added
- `docs-site/getting-started/` — a hands-on "Getting started" tutorial page that runs the `Foundgine.SupplyChain` sample end to end and walks through its ten architectural layers (API → Application → Domain → AOT → Semantics → Query/Mutation repositories → high-assurance mutations → MCP → Testing), following the sample's `GUIDE.md`. Linked from the site nav, `sitemap.xml`, `llms.txt`, and `llms-full.md`.

### Fixed
- **`samples/Foundgine.SupplyChain/Domain/Foundgine.SupplyChain.Domain.csproj` was missing the `Foundgine.Aot.Generator` analyzer reference.** The project declares `[FoundgineModel]`/`[FoundgineEntity]`/`[FoundgineField]`-attributed types but only referenced `Foundgine.Aot` as a plain `ProjectReference`, which does not transitively add `Foundgine.Aot.Generator` as an analyzer to `Domain`'s own compilation. The generator therefore never ran for `Domain`, so `Foundgine.Generated.GeneratedMetadata` (consumed by `Semantics/SupplyChainSemanticModel.cs` and, downstream, `Infrastructure` and `Tests`) was never emitted, and the sample failed to compile with `CS0234` on `Foundgine.Generated`. Added the missing `<ProjectReference Include="../../../src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all" />` entry, matching the pattern already used correctly in `tests/Foundgine.Aot.Tests` and `tests/Foundgine.E2E.Tests`.
- `docs-site/index.html` — the three "problem" callouts under the homepage hero (`Too many bespoke tools`, `Too much agent work`, `Execution rules get fragmented`) had no separator between the bold lead-in and the following sentence in the markup, so the two ran together as a single word (e.g. `toolsBusiness`) in contexts where the `.problem-grid strong`/`span` block-display CSS isn't applied. Added terminal punctuation and a space so the text reads correctly regardless of rendering context.

### Known verification gap
- The `Domain.csproj` fix above has not been verified with an actual `dotnet build`/`dotnet test` run in the environment that prepared this release (no .NET SDK available there). It should be verified in CI, or locally with `dotnet build samples/Foundgine.SupplyChain/Foundgine.SupplyChain.sln`, before this version is tagged and packages are published. No `src/` package code changed in this release, so the existing 0.5.0 restore/build/test evidence for the packages themselves still stands.

## [0.5.0]

### Changed
- **`Foundgine.Security.Authority` promoted to a real, packaged library.** The authorization recovery control plane — witness quorum, credential lifecycle, journal reconciliation, and failover — moved out of `samples/Foundgine.HighAssurance.Postgres/Authorization/` and `.../Execution/` into a new `src/Foundgine.Security.Authority/` project, under a single `Foundgine.Security.Authority` namespace, depending only on `Foundgine.Execution` and the BCL. The two files that hardcode the sample's `transferFunds` operation (`AuthorizationDecision.cs`, `AuthorizationExecutionBinding.cs`) and the four genuinely Postgres-specific files (`PostgresAuthorizationContextStore`, `PostgresAuthorizationRecoveryCoordinator`, `PostgresAuthorizationSecurityUnitOfWork`, `PostgresTransferFundsExecutor`) stayed in the sample.
- **`Foundgine.sln` fixed.** Removed a duplicate `ProjectConfigurationPlatforms` block that sat outside any `GlobalSection`, added the missing `Release|Any CPU` build configuration for the Banking/Postgres sample projects, and registered the new `Foundgine.Security.Authority` project.
- **Milestone-numbering scheme removed from public surfaces.** Internal tracking IDs previously embedded in doc comments, README section headers, and changelog entries carried no meaning outside the original development process and are now gone; section headers use plain descriptive titles instead.
- **Documentation index rewritten.** `docs/README.md` no longer links to files that don't exist in this repository. The same dead-link and stale-path cleanup was applied to `docs/ROADMAP.md`, `docs/SECURITY.md`, `the active Security.Authority test suite`, `README.md`, `ai.seo.md`, and `llms-full.md`.

### Fixed
- Test files under `tests/Foundgine.Security.Authority.Tests` that reference the relocated `Foundgine.Security.Authority` types were missing the corresponding `using Foundgine.Security.Authority;` directive after the move; added.

## [0.4.0]

### Added
- **Authorization recovery control plane.** Adds authorization-recovery handling covering the failure and recovery paths of the authorization control plane: publication key lifecycle, rotation and retirement, promotion and commit atomicity, cross-instance commit and journal consensus/reconciliation, repair ordering and idempotency, and repair-proposer credential authentication, lifecycle, and replication. Full invariant-by-invariant detail lives in `docs/security/`; adversarial coverage lives in `tests/Foundgine.Security.Authority.Tests/`.
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


## Step 18 — Semantic Contract Attestation

- Added `SemanticContractAttestation` for fail-closed AOT/runtime contract verification.
- Exposed `GeneratedSemanticModel.ContractFingerprint` from generated metadata.
- Added regression coverage for generated/runtime fingerprint equivalence.

## Step 30 follow-up — Dynamic intent convergence

- Added immutable-contract binding to `ReadIntentCompiler`.
- Added `CompileOperationGraph(ReadIntent)` for dynamic intent to canonical IR convergence.
- Added contract fingerprint exposure for runtime provenance.
- Added regression coverage proving deep logical traversals converge on the same operation-graph fingerprint and planner input.

## Step 31 — Semantic Intent Document & Resolution Contract

Added a contract-bound, versioned `SemanticIntentDocument` for dynamic/runtime intents. Documents bind caller-produced intent to the frozen semantic contract fingerprint and are rejected on contract mismatch before semantic resolution. Added explicit `SemanticIntentResolution` evidence and graph resolution helpers, plus regression coverage proving direct and document-based intents converge on the same operation-graph fingerprint.

## Step 32 — Semantic Traversal Safety & Resource Bounds

- Added provider-neutral operation-graph node, depth, edge, and field limits.
- Added canonical `SemanticOperationGraphSafetyValidator`.
- Added topology consistency, cycle/repeat, and unreachable-node checks.
- Applied graph safety to dynamic `ReadIntentCompiler.CompileOperationGraph` and planner graph entry points.

## Step 33 — Graph-Level Authorization

- Added explicit `SemanticOperationGraph` authorization APIs.
- Added `SemanticOperationGraphAuthorizationResult` with contract-bound evidence.
- Added regression coverage for denied relationship subtrees and contract-bound evidence.
- Added `SEMANTIC-GRAPH-AUTHORIZATION-STEP33.md`.