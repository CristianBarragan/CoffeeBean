# Current direction

Foundgine 0.3.0 is the current shipped release. The repository is organized around a stable semantic execution boundary rather than a numbered milestone program.

## What the active codebase is focused on

### Semantic execution

The core pipeline is:

```text
Caller
  ↓
Structured intent
  ↓
Semantic resolution
  ↓
Authorization
  ↓
Semantic plan
  ↓
Plan validation / rewriting
  ↓
Provider execution
  ↓
Result + evidence
```

The semantic layer is deliberately independent of GraphQL, SQL, AI frameworks, and transport concerns.

### Authorization and security

Authorization is represented as application-defined semantic policy and carried into planning and execution. Security invariants are part of the execution contract. Provider capabilities must be able to preserve the required guarantees before execution is allowed.

Foundgine does not claim to solve authentication, identity management, secrets, transport security, rate limiting, database permissions, or deployment security.

### Provider execution

The repository contains SQL and InMemory execution paths, with PostgreSQL integration and end-to-end coverage where the environment is configured. Provider independence means the semantic meaning is not expressed as SQL; providers translate the logical execution model into their own physical work.

### Planning and optimization

The planner contains conservative rewrite and optimization infrastructure. Transformations are expected to preserve semantic meaning and security constraints. Optimization is therefore a proof-bearing part of planning, not a license to alter business meaning.

### AI and structured callers

AI agents are treated as untrusted producers of structured intent. Trusted execution context remains host-owned. The agent does not choose tenant identity, authorization context, provider, or database connection.

The project also exposes JSON and GraphQL adapters that converge on the same semantic execution boundary.

## Evidence over roadmap claims

The active source tree and active tests are the source of truth. Performance numbers, security properties, provider support, and examples should only be presented when they are implemented and backed by repository evidence.

Detailed numbered milestone documents are retained under `docs/history/milestones` as development history. They are not a statement of current or future priority.

## Areas for continued development

The repository can continue to deepen: provider coverage, public API ergonomics, semantic actions, authorization integrations above the policy boundary, agent integrations, observability, and end-to-end measurement. These are areas of development rather than promised milestone commitments.
