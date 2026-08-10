# Vision

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

## Target lifecycle

```text
DOMAIN MODEL
     ↓
SEMANTIC MODEL
     ↓
STRUCTURED AI INTENT
     ↓
RESOLUTION
     ↓
POLICY / AUTHORIZATION
     ↓
EXECUTION PLAN
     ↓
PREVIEW
     ↓
EXECUTE
     ↓
VERIFY
     ↓
EVIDENCE
```

## Current reality

Today the repository proves the lower execution path and substantial parts of the semantic/read path.

The target lifecycle above is therefore a **direction diagram**, not a claim that every box is production-complete.

## The boundary

```text
AI reasoning
     ↓
structured intent
     ↓
Foundgine
     ↓
safe executable plan
```

The AI does not get arbitrary SQL or arbitrary CLR invocation.

## Long-term role of Roslyn

Roslyn may eventually generate the semantic vocabulary of the application:

```text
Entities
Relationships
Search descriptors
Actions
Policies
Planner hints
```

It should not generate fixed plans for unknown future natural-language requests.

## Non-goals

Foundgine should not become:

- an LLM;
- a general agent framework;
- a RAG platform;
- an MCP implementation;
- an ORM;
- a workflow engine;
- a message broker.
