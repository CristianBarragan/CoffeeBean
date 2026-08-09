# Foundgine Documentation

Foundgine is a .NET application-domain semantic and execution platform for AI-native applications.

> **Foundgine turns an application's domain model into a safe, executable interface for AI agents.**

## Start here

1. [Direction](00-Direction/README.md)
2. [Proof Milestones](00-Direction/Milestones.md)
3. [Current Status](CURRENT-STATUS.md)
4. [Architecture](02-Architecture/README.md)
5. [Banking E2E Sample](11-Samples/README.md)

## Core documentation

### [Direction](00-Direction/README.md)

The product boundary and why Foundgine should not become another AI/RAG/MCP framework.

### [Architecture](02-Architecture/README.md)

How domain semantics, planning and execution fit together.

### [Foundation](03-Foundation/README.md)

Stable platform primitives and dependency rules.

### [Runtime](04-Runtime/README.md)

Execution contracts and the current runtime model.

### [Persistence](08-Persistence/README.md)

Database/provider execution boundaries.

### [AI Integration](09-AI/README.md)

How Foundgine fits beneath an LLM or agent framework.

### [Samples](11-Samples/README.md)

The Banking sample is the canonical proof.

### [Reference](13-Reference/README.md)

ADRs, glossary, changelog and roadmap.

## Historical material

The repository contains historical GraphQL/Graphgine material under `archive/`.

The following documentation sections are retained for migration/history:

- [GraphQL](05-GraphQL/README.md)
- [Source Generators](06-Source-Generators/README.md)

They should not be used as the current product definition.

## Current milestone chain

```text
M0  Real execution                         ← current proof
 ↓
M1  Semantic domain
 ↓
M2  Resolution
 ↓
M3  Read intent
 ↓
M4  Domain actions
 ↓
M5  Policy / authorization
 ↓
M6  Preview / approval
 ↓
M7  Verification / evidence
 ↓
M8  MCP adapter
 ↓
M9  More execution targets
 ↓
M10 Compile-time semantic compiler
```

See [Proof Milestones](00-Direction/Milestones.md).

## Documentation accuracy

Documentation must distinguish:

- **Implemented** — executable and proven.
- **In progress** — partially implemented.
- **Planned** — architectural direction only.
- **Historical** — retained for context but not current.

Do not turn architectural intent into feature claims.
