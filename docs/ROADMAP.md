# Roadmap

Foundgine 0.3.0 is the current shipped release. The core semantic execution pipeline is now validated by restore, build, and the full automated test suite. The roadmap therefore focuses on usefulness, provider depth, public API clarity, and evidence rather than another architecture-freeze cycle.

## Current foundation — shipped

The current release includes the validated foundation for:

- semantic modeling and request resolution;
- granular authorization and authorization-aware planning;
- query and mutation planning;
- execution IR and provider lowering;
- SQL and InMemory execution;
- GraphQL and JSON adapters;
- AOT metadata generation;
- MCP boundary and mutation-safe execution;
- execution receipts and plan-bound approval;
- deterministic plan fingerprints and provider-plan caching; and
- PostgreSQL integration and benchmark workflows.

The former M39–M42 material remains available in the implementation history and detailed architecture documents; it is no longer the primary public roadmap.

## Next

### Public API simplification

Reduce unnecessary surface complexity while preserving the semantic/execution boundaries that are now validated.

### Provider depth

Improve provider composition and extend real-world provider scenarios without weakening the provider-independent semantic model.

### PostgreSQL hardening

Expand real PostgreSQL E2E coverage, mutation coverage, and repeatable benchmark methodology.

### Developer experience

Improve examples, getting-started material, package documentation, and diagnostics around plan inspection and execution evidence.

### AI and MCP integration

Expand safe capability discovery and caller integration while keeping authorization and execution authority inside Foundgine.

## Later

Potential future work includes additional providers, richer semantic actions, claims/roles integration above the policy contract, parameterized plan templates, and deeper AI/agent integration.

These are future directions, not current core guarantees.

## Roadmap rule

The active source and tests are the source of truth. Public documentation must distinguish shipped/tested capabilities from planned work and historical material. See [Documentation truth](DOCUMENTATION-TRUTH.md).
