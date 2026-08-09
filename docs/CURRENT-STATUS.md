# Current Status

[Home](../README.md) → **Current Status**

## Executive status

Foundgine has a real lower-level execution proof, but the AI-native product surface is not yet implemented end to end.

The repository should therefore be treated as:

> **A working execution substrate plus an active proof of the AI-native semantic layer.**

## What is real today

The canonical Banking sample proves:

```text
Metadata
  ↓
Dynamic Planner
  ↓
Logical QueryPlan
  ↓
ProviderPlan
  ↓
SQL
  ↓
real SQLite database
  ↓
ExecutionRow result
```

The sample deliberately has no GraphQL dependency.

Current active platform projects include:

| Project | Role | Status |
|---|---|---|
| `Foundgine.Abstractions` | stable contracts | active |
| `Foundgine.Foundation` | primitives and generic CQRS contracts | active |
| `Foundgine.Metadata` | entity/column/join metadata | active |
| `Foundgine.Diagnostics` | diagnostic infrastructure | active |
| `Foundgine.Builders` | logical query-plan structures | active |
| `Foundgine.Execution.Contracts` | execution/provider contracts | active |
| `Foundgine.Planning` | dynamic planning and mutation plan structures | active |
| `Foundgine.Providers` | provider compilation/execution | active, incomplete |
| `Foundgine.Samples.Banking` | canonical E2E proof | active |

## What is not yet proven

The following are target capabilities, not completed features:

- natural-language intent integration
- semantic entity resolution
- action discovery
- domain-action execution
- policy-aware planning
- preview/approval
- post-execution verification
- evidence model
- MCP adapter
- compile-time semantic domain compiler
- semantic retrieval target
- external-data execution target

## What is intentionally not being built

Foundgine is not becoming:

- an LLM provider
- a general-purpose agent framework
- a RAG framework
- a vector database
- an MCP implementation
- an ORM
- a workflow engine
- a message broker

Those are integration points.

## Evidence standard

Documentation must distinguish three states:

### Implemented

There is executable code and an automated or real integration proof.

### In progress

The architecture and partial code exist, but the full behavior is not proven.

### Planned

The capability is part of the roadmap but should not be described as existing.

Avoid "production ready", "fully AOT compatible", "zero reflection", "database independent", or performance claims until CI and benchmarks establish them.

## Immediate priorities

1. Keep the Banking E2E green.
2. Introduce a protocol-neutral semantic model.
3. Add deterministic entity resolution.
4. Add a read-intent-to-plan path.
5. Add explicit domain actions.
6. Add policy evaluation.
7. Add preview/approval for mutations.
8. Add verification and evidence.
9. Expose the semantic surface through MCP.
10. Only then invest heavily in compile-time generation.

## Success criterion

The first meaningful product milestone is not "many features".

It is one complete read and one complete mutation:

```text
READ
"Find Ada's last five transactions."
```

and:

```text
MUTATION
"Refund Ada's last transaction."
```

Both must operate against a real application domain and produce inspectable evidence.
