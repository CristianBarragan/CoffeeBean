[Home](../../README.md) → [Documentation](../README.md) → [Architecture](README.md) → **Vision**

# Vision

## The statement

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

The objective is not to make another AI framework.

The objective is to make the application's existing domain understandable and executable by AI without exposing arbitrary infrastructure.

## The problem

An agent can understand:

> "Refund John's last payment."

But it does not inherently know:

- which object is John
- which payment is "last"
- whether a refund is legal
- which business action performs the refund
- what side effects occur
- how to verify the result

Foundgine owns that application-specific boundary.

## Architecture

```text
C# Domain
   ↓
Semantic Domain Model
   ↓
AI Intent
   ↓
Resolution
   ↓
Policy
   ↓
Execution Plan
   ↓
Preview
   ↓
Execute
   ↓
Verify
   ↓
Evidence
```

## What makes this different

The domain is the source of truth.

The AI does not become the source of truth.

The AI proposes intent. Foundgine constrains and executes that intent according to the application domain.

## Non-goals

Foundgine should not become:

- an LLM provider
- a general agent framework
- a RAG framework
- an MCP implementation
- an ORM
- a workflow engine
- a message broker

Those systems can integrate with Foundgine.

## Long-term direction

The most important interface is:

```text
Application domain
       ↕
Foundgine semantic API
       ↕
AI agent
```

GraphQL, REST, gRPC and MCP are possible outer adapters.

## Current reality

The lower execution substrate is proven in the Banking sample.

The semantic/AI lifecycle is the active roadmap.

See [Proof Milestones](../00-Direction/Milestones.md).
