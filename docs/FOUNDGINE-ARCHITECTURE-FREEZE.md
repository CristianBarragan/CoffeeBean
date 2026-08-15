# Foundgine Architecture — Phase 12 Consolidation

## Status

**Architecture freeze candidate**

This document consolidates the execution architecture established through Phase 11.
It is intentionally conservative: existing domain concepts remain authoritative and
adapter-specific protocols do not become core semantics.

## 1. Core model

```text
External Actor
   |
   +-- GraphQL
   +-- JSON
   +-- MCP
   +-- Application API
   |
   v
Intent
   |
   v
Semantic Model
   |
   +-- Capabilities
   +-- Actions
   +-- Relationships
   +-- Constraints
   +-- Effects
   |
   v
Authorization
   |
   v
Authorized Semantic Plan
   |
   v
Policy-Aware Optimization
   |
   v
Execution IR / Provider Lowering
   |
   v
Provider Execution
   |
   v
ExecutionReceipt
```

## 2. Canonical responsibilities

### Semantic Model
The semantic model is the authoritative description of application meaning:
entities, fields, relationships, operations, capabilities, and semantic versions.

### Intent
Intent is an untrusted request describing what an actor wants to accomplish.
Intent is never an authorization decision.

### Capability Contract
The capability contract describes what the application exposes semantically:
operation, target, inputs, constraints, effects, relationships, and versions.

### Authorization
Authorization determines whether the current actor/context may perform the requested
semantic operation. It remains a security boundary and is never delegated to MCP,
AI, providers, or optimizers.

### Plan
The plan is the authorized semantic execution representation. It is the artifact that
dry-run, approval, optimization, execution, and evidence refer to.

### Optimization
Optimization may canonicalize and improve an already-authorized plan. It may not grant
access or remove authorization constraints.

### Approval
Approval binds a human/authority decision to an exact plan fingerprint and semantic
version context. Approval is not a replacement for authorization.

### Execution
Execution occurs only after authorization checks and, where required, exact-plan
approval verification.

### ExecutionReceipt
ExecutionReceipt is canonical evidence for both reads and mutations. It is not an
authority mechanism.

## 3. Adapter rule

Adapters translate external protocols into Foundgine semantics:

```text
MCP      -> Intent / Capability discovery
GraphQL  -> Intent
JSON     -> Intent
```

No adapter owns:

- business authorization
- provider access
- semantic planning
- mutation execution rules
- independent capability definitions

## 4. Provider rule

Providers implement execution/lowering capabilities.

Providers must never become an alternate semantic authorization boundary.

```text
Foundgine semantic plan
        |
        v
provider lowering
        |
        v
provider execution
```

## 5. Trust boundaries

```text
UNTRUSTED
  external request
  AI-generated intent
  MCP arguments
       |
       v
VALIDATION / RESOLUTION
       |
       v
AUTHORIZATION BOUNDARY
       |
       v
AUTHORIZED PLAN
       |
       +--> optimization
       |
       +--> dry run
       |
       +--> approval
       |
       v
EXECUTION
       |
       v
EVIDENCE
```

## 6. Versioning

The following version identities are distinct:

- SemanticModelVersion
- CapabilityContractVersion
- CapabilityVersion
- IntentVersion
- PlanVersion

Fingerprints identify exact artifacts; versions identify the semantic/protocol
interpretation under which those artifacts were produced.

## 7. Approval invariant

An approved execution must verify:

```text
current semantic model
        == approved semantic model context

current capability contract
        == approved capability context

current intent
        == approved intent identity

current authorized plan
        == approved plan fingerprint
```

Any mismatch invalidates approval.

## 8. Receipt invariant

A receipt records execution evidence but never grants authority.

For mutations it may additionally record:

- approval identity
- affected semantic nodes
- semantic effects
- result fingerprint

Reads and mutations therefore share one evidence contract.

## 9. AOT implications

AOT generation should generate semantic metadata and deterministic plan artifacts,
not duplicate authorization logic.

The runtime remains responsible for request-specific authorization context.

This preserves:

```text
AOT:
  semantic structure
  deterministic planning metadata

Runtime:
  actor/context
  authorization decision
  request values
  execution
```

## 10. Public API discipline

New features must satisfy:

1. Can the concept be expressed using an existing semantic abstraction?
2. Does the concept belong in core semantics rather than an adapter?
3. Does it create a second source of truth?
4. Does it introduce a new authorization path?
5. Does it duplicate an existing plan/evidence representation?

If yes to 3–5, redesign before adding public API surface.

## 11. Current implementation inventory

Projects discovered in this consolidation:

- `foundgine_phase10_final/tests/Foundgine.MCP.Tests/Foundgine.MCP.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.Aot.Tests/Foundgine.Aot.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.Intent.Json.Tests/Foundgine.Intent.Json.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.InMemory.Tests/Foundgine.InMemory.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.Planning.Tests/Foundgine.Planning.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.Security.Tests/Foundgine.Security.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.Semantics.Tests/Foundgine.Semantics.Tests.csproj`
- `foundgine_phase10_final/tests/Foundgine.GraphQL.HotChocolate.Tests/Foundgine.GraphQL.HotChocolate.Tests.csproj`
- `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj`
- `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/CoffeeBeanery.LoadTest/CoffeeBeanery.LoadTest.csproj`
- `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/Foundgine.CoffeeBeanery.BenchmarkApi/Foundgine.CoffeeBeanery.BenchmarkApi.csproj`
- `foundgine_phase10_final/benchmarks/CoffeeBeanery.Performance/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj`
- `foundgine_phase10_final/src/Foundgine.Planning/Foundgine.Planning.csproj`
- `foundgine_phase10_final/src/Foundgine.MCP/Foundgine.MCP.csproj`
- `foundgine_phase10_final/src/Foundgine.Intent.Json/Foundgine.Intent.Json.csproj`
- `foundgine_phase10_final/src/Foundgine.Metadata/Foundgine.Metadata.csproj`
- `foundgine_phase10_final/src/Foundgine.Semantics/Foundgine.Semantics.csproj`
- `foundgine_phase10_final/src/Foundgine.InMemory/Foundgine.InMemory.csproj`
- `foundgine_phase10_final/src/Foundgine.Aot.Generator/Foundgine.Aot.Generator.csproj`
- `foundgine_phase10_final/src/Foundgine.AI/Foundgine.AI.csproj`
- `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate/Foundgine.GraphQL.HotChocolate.csproj`
- `foundgine_phase10_final/src/Foundgine.GraphQL.HotChocolate.Mutations/Foundgine.GraphQL.HotChocolate.Mutations.csproj`
- `foundgine_phase10_final/src/Foundgine.Sql/Foundgine.Sql.csproj`
- `foundgine_phase10_final/src/Foundgine.Execution/Foundgine.Execution.csproj`
- `foundgine_phase10_final/src/Foundgine/Foundgine.csproj`
- `foundgine_phase10_final/src/Foundgine.Aot/Foundgine.Aot.csproj`
- `foundgine_phase10_final/src/Foundgine.Abstractions/Foundgine.Abstractions.csproj`
- `foundgine_phase10_final/samples/Foundgine.Agent.OpenAI/Foundgine.Agent.OpenAI.csproj`

Source files discovered: **297**

Documentation files discovered: **190**

## 12. Freeze boundary

The architecture should now freeze these invariants before adding major features:

- one semantic model
- one capability contract
- one intent boundary
- one authorization boundary
- one canonical plan identity
- one approval model
- one execution receipt
- adapters remain transport/protocol boundaries
- providers remain execution boundaries

## 13. Next development after freeze

The next work should be implementation hardening rather than another conceptual layer:

1. compile the complete solution
2. resolve duplicate/overlapping abstractions
3. integrate all receipt paths
4. validate MCP read/write integration
5. execute the security/fuzz suite
6. add provider integration tests
7. update README and AI discovery documentation
8. benchmark the new planning/authorization path
9. then consider durable workflows
