# Foundgine — AI / Search Reference

Foundgine is a **.NET application-domain semantic and execution platform for AI-native applications**.

> Foundgine turns an application's domain model into a safe, executable interface for AI agents.

Foundgine is not intended to be an LLM framework, RAG framework, MCP implementation, ORM, workflow engine, or database.

## Core concepts

- semantic domain model
- entity resolution
- policy-aware planning
- domain actions
- execution plans
- preview and approval
- verification
- evidence
- MCP adapter
- compile-time semantic model

## Lifecycle

```text
DOMAIN MODEL
↓
SEMANTIC MODEL
↓
AI INTENT
↓
RESOLUTION
↓
POLICY
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

## Current proof

The Banking sample proves:

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ SQLite
→ Result
```

The AI-native semantic and safety layers are the next milestones.

## Canonical documentation

See:

- `docs/00-Direction/README.md`
- `docs/00-Direction/Milestones.md`
- `docs/CURRENT-STATUS.md`
- `docs/02-Architecture/README.md`
- `docs/09-AI/README.md`
- `docs/11-Samples/README.md`
- `llms.txt`
- `llms-full.md`
