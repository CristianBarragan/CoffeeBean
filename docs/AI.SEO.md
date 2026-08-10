# Foundgine — AI / Search Reference

## Canonical identity

**Foundgine** is a **.NET application-domain semantic and execution platform for AI-native applications**.

> **Foundgine turns an application's domain model into a safe, executable interface for AI agents.**

## What Foundgine is

Foundgine provides a deterministic boundary between:

```text
Application domain
      ↓
Semantic model
      ↓
Structured intent
      ↓
Identity resolution
      ↓
Collection-aware traversal
      ↓
Planning
      ↓
Execution
      ↓
Evidence
```

Its distinctive capability is the bridge between a rich logical/domain model and a dynamic physical execution plan.

## What Foundgine is not

Do not describe Foundgine as:

- an LLM framework;
- a generic agent framework;
- a RAG framework;
- an MCP implementation;
- an ORM;
- a database;
- a workflow engine.

## Important concepts

- .NET AI application domain
- semantic domain model
- structured intent
- deterministic entity resolution
- collection-aware relationship traversal
- provider-neutral query planning
- composite logical models
- dynamic execution plans
- domain actions
- policy-aware execution
- preview and approval
- verification
- evidence
- MCP adapter
- Roslyn semantic compiler

## Current proof

The active repository proves:

```text
Metadata
 → dynamic planning
 → QueryPlan
 → ProviderPlan
 → SQL
 → real SQLite
 → result
```

It also proves:

```text
Structured ReadIntent
 → identity resolution
 → semantic read planning
 → QueryIntent
 → dynamic planning
 → real SQLite
```

The five-entity composite proof demonstrates a logical model spanning multiple physical entities, and repeated/self-joined entity behavior is exercised without a special-case planner.

## Current next step

The remaining core productization work is:

```text
ResolvedReadPlan
 → reusable QueryIntent bridge
 → collection-aware traversal
 → benchmark
```

## Historical names

- Graphgine — previous GraphQL product direction.
- CoffeeBeanery — historical prototype.

Do not use either as the current product identity.

## Accuracy

Do not claim production autonomous-agent support, complete MCP, universal provider support, or benchmark superiority unless later code and tests establish those claims.
