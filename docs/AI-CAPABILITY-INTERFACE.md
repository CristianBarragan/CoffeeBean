# AI / Capability Interface

Foundgine exposes semantic capability discovery for callers such as AI agents, APIs, and application tooling.

The capability interface is deliberately **not an AI framework**. It describes what the configured semantic model and authorization policy make available to a caller. It does not grant access and it does not produce provider instructions.

## Contract

```text
Capability discovery
        ↓
Structured semantic intent
        ↓
Semantic resolution
        ↓
Authorization
        ↓
Execution plan
        ↓
Provider
        ↓
Evidence
```

The public entry point is:

```csharp
var capabilities = foundgine.DescribeCapabilities();
```

`DescribeCapabilities()` returns the current policy-scoped semantic capability graph:

```text
Entities
 ├── entity read/write access
 ├── fields
 │    └── read/write access
 └── relationships
      └── read/write access
```

## Security invariant

Capability discovery is **descriptive**.

An agent does not gain permission by discovering a capability. The normal execution pipeline evaluates authorization again after the agent produces a `SemanticRequest`.

```text
Capability document ≠ authorization decision
```

Conditional authorization is intentionally represented only as `Conditional`; the underlying authorization predicate is not exposed by capability discovery. This prevents a capability document from becoming a policy-leak or authorization bypass.

## Why this is enough for the core

The core should know about:

- semantic entities;
- fields;
- relationships;
- read/write capability state;
- structured intent;
- authorization;
- execution;
- evidence.

The core should **not** know about:

- LLM providers;
- prompts;
- conversations;
- memory;
- agent orchestration;
- MCP clients;
- model-specific tool calling.

AI/MCP integrations belong above the semantic capability interface.

## Determinism

Capability discovery is deterministic for the same semantic model and authorization policy: entities, fields, and relationships are returned in stable name order.

This makes the result suitable for machine consumption, caching by an adapter, and reproducible tests without making capability discovery itself an authorization cache.

## Recommended agent flow

An AI adapter can use the interface as follows:

```text
1. Discover capabilities
2. Construct SemanticRequest
3. Submit request through IFoundgine
4. Let Foundgine resolve and authorize it
5. Execute through the configured provider
6. Inspect ExecutionEvidence when available
```

The agent never needs to generate SQL, provider plans, table names, joins, or authorization predicates.

## Deliberate non-goals

Foundgine does not attempt to become an agent framework. A future `Foundgine.AI` or `Foundgine.MCP` adapter could translate external tool/capability formats into Foundgine semantic intent, while keeping the core independent of those technologies.
