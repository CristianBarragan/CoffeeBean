# AI Integration

[Home](../../README.md) → [Documentation](../README.md) → **AI**

Foundgine is AI-native at the application boundary, but it is not an AI model framework.

## Position

```text
LLM / Agent framework
        │
        ▼
      Intent
        │
        ▼
    Foundgine
        │
        ├── semantic resolution
        ├── policy
        ├── planning
        ├── execution
        ├── verification
        └── evidence
```

The external AI system owns reasoning and conversation.

Foundgine owns application semantics and safe execution.

## Why this boundary matters

A generic AI system does not automatically know:

- which entities exist
- which relationships are legal
- which operations are business actions
- which actions mutate state
- who may perform them
- how to verify the result

Those facts belong to the application.

## AI integration principles

### 1. Constrained vocabulary

The model should select from a known semantic vocabulary.

### 2. No arbitrary method invocation

Foundgine must never translate an LLM-generated method name into arbitrary CLR invocation.

### 3. Plan before mutation

Mutations should become explicit plans before execution.

### 4. Policy before execution

Authorization is part of planning.

### 5. Evidence after execution

The agent should receive structured evidence rather than trusting an unverified textual claim.

## MCP

MCP is a planned outer adapter:

```text
Agent
 ↓
MCP
 ↓
Foundgine semantic API
```

Foundgine does not need to implement the MCP protocol inside its core.

## Retrieval

Semantic retrieval is a planned execution target, not the definition of Foundgine.

A future system can combine:

```text
Structured data
+
Semantic retrieval
+
Domain actions
+
External data
```

under the same planning/evidence model.

## AI accuracy rules

Never claim that Foundgine currently has:

- a complete agent framework
- natural-language planning in production
- complete MCP support
- complete RAG support
- autonomous mutation safety
- enterprise-grade authorization
- production-ready evidence/audit

Those are roadmap capabilities until demonstrated by code and tests.

See:

- [Direction](../00-Direction/README.md)
- [Proof Milestones](../00-Direction/Milestones.md)
- [Current Status](../CURRENT-STATUS.md)
- [LLM Readiness](LLM-Readiness.md)
