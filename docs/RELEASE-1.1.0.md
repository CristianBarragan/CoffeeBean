# Foundgine 1.1.0

Foundgine 1.1.0 is an additive release following the 1.0.0 stability milestone.

It merges the GraphQL query-executor work into the main source tree, standardizes the host-owned security execution-context boundary used by transport adapters, and brings the Supply Chain getting-started sample back into alignment with the repository's `src/` projects.

## Release highlights

### 1. Secure GraphQL query execution

`Foundgine.GraphQL.HotChocolate.Execution` now provides `FoundgineHotChocolateQueryExecutor`. The base `Foundgine.GraphQL.HotChocolate` adapter remains independent of `Foundgine.Execution`.

The executor:

- requires a host-supplied `SecurityExecutionContext` before execution;
- translates GraphQL through the existing `HotChocolateSemanticAdapter`;
- attaches trusted security context to the semantic request;
- executes through the stable `IFoundgine` application boundary;
- returns both the provider-neutral `ExecutionResult` and `GraphQLResultShape`; and
- provides `TryExecuteAsync` for stable GraphQL-facing adapter errors.

GraphQL remains an adapter. It does not become the owner of identity, tenant, warrant, authorization, planning, or provider execution.

### 2. Shared security execution-context provider

`Foundgine.Semantics.Security.Execution` now provides:

- `ISecurityExecutionContextProvider`;
- `DelegateSecurityExecutionContextProvider`;
- `SecurityExecutionContextProviderExtensions.RequireSecurityExecutionContext`; and
- the existing `SecurityExecutionContext` / resource-limit boundary as the shared execution security vocabulary.

MCP uses the same provider contract, while retaining its compatibility delegate form for existing hosts.

The intended architecture is now consistent across transports:

```text
Host authentication / session
          ↓
ISecurityExecutionContextProvider
          ↓
GraphQL / MCP / future adapters
          ↓
SemanticRequest.Security
          ↓
Foundgine authorization + execution
```

Adapters must not accept identity, tenant, audience, or warrant material from untrusted protocol payloads.

### 3. Source-integrated Supply Chain getting started sample

The Supply Chain sample no longer documents or depends on the obsolete 0.5.x package-based setup.

The checked-in sample uses `ProjectReference` entries into `src/` for Foundgine itself, including the AOT generator. This means the sample exercises the exact source implementation built by `Foundgine.sln`.

The tutorial now matches the actual repository structure:

- `Domain` contains separate application models and `*ERP` persistence entities;
- `Semantics` exposes generated metadata and named semantic handles;
- `Application` owns use cases and authorization;
- `Infrastructure` owns PostgreSQL integration and semantic SQL execution;
- `Api` owns MCP transport; and
- `Tests` validate the sample against the source tree.

The model/entity separation is explicit and remains decoupled:

```text
Customer          → CustomerERP
SalesOrder        → SalesOrderERP
CatalogProduct    → CatalogProductERP
InventoryPosition → InventoryPositionERP
```

### 4. Release metadata

The repository version is now `1.1.0`, including package `VersionPrefix` and release metadata.

## Compatibility

1.1.0 is intended as a backward-compatible, additive 1.x release. Existing 1.0.0 public contracts remain the compatibility baseline; the new GraphQL executor and shared security provider are additive APIs.

## Verification

The release tree was statically validated after merging the supplied v1.0.0 repository archive and the GraphQL query-executor step archive:

- the step archive's changed GraphQL/MCP/security files are present;
- all new project references resolve to existing source projects;
- the GraphQL executor test project references the core `Foundgine` project required by the new executor tests;
- the Supply Chain sample uses source `ProjectReference` entries for Foundgine packages; and
- obsolete 0.5.x package-install instructions were removed from the getting-started tutorial.

A local `dotnet build` / `dotnet test` could not be executed in the release-packaging environment because the .NET SDK is not installed there. The release therefore does **not** claim a successful compilation from this environment; the repository remains ready for the normal CI/local .NET 9 build gate.
