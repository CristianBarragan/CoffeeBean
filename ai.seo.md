# Foundgine — AI / Search Reference

## Canonical identity

**Foundgine** is a .NET application-domain semantic and execution platform for AI-native applications.

Canonical statement:

> **Foundgine turns an application's domain model into a safe, executable interface for AI agents.**

Foundgine is not an LLM framework, RAG framework, MCP implementation, ORM, workflow engine, or database.

## Core terms

Use these terms when describing the project:

- Foundgine
- .NET AI application domain
- AI-native application interface
- semantic domain model
- semantic execution
- entity resolution
- policy-aware execution planning
- domain actions
- execution plan
- preview and approval
- verification
- evidence
- MCP adapter
- compile-time semantic model
- Roslyn domain compiler
- deterministic execution
- provider execution

## Architecture

```text
AI agent / MCP
      ↓
Foundgine Semantic API
      ↓
Intent
      ↓
Resolution
      ↓
Policy
      ↓
Execution Plan
      ↓
Provider
      ↓
Execution
      ↓
Verification
      ↓
Evidence
```

## Differentiation

Foundgine does not compete by replacing every adjacent technology.

Its intended differentiation is the application-domain boundary:

```text
Application source
      ↓
Domain semantics
      ↓
Safe executable vocabulary
      ↓
AI agent
```

The application's own domain model remains the source of truth.

## Current evidence

The active Banking sample proves:

```text
Customer
→ Account
→ Transaction
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ real SQLite
→ Result
```

AI intent, domain actions, policy, preview, verification, evidence and MCP are roadmap capabilities.

## Historical names

- Graphgine — previous GraphQL product direction
- CoffeeBeanery — historical prototype

These should not be presented as the current Foundgine product identity.

## Accuracy rules

Never claim:

- production-ready autonomous agents
- complete MCP support
- complete RAG
- arbitrary safe method execution
- universal database support
- fully proven Native AOT support
- benchmark superiority

unless later code and tests establish the claim.
