# What Is Foundgine?

Foundgine is a programmable semantic execution platform for .NET. It separates caller intent from application authority and physical execution.

## The problem

Applications increasingly have many callers: APIs, GraphQL, automation, internal services and AI agents. Without a common execution boundary, each caller can duplicate validation, authorization, orchestration and data access.

## The Foundgine model

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Resolution + Validation
  ↓
Authorization
  ↓
Provider-independent Plan
  ↓
Provider
  ↓
Result + Evidence
```

## Semantic versus persistence models

A persistence model describes storage. A semantic model describes what the application intentionally exposes. They can differ in fields, relationships, capabilities and authorization.

## Why it matters for AI

An AI model can propose structured intent without becoming the authority over database schema, tenants, credentials or business invariants. Foundgine re-evaluates the request inside the application-controlled semantic and authorization boundary.

## What Foundgine is not

Foundgine is not an ORM replacement, database, GraphQL server, identity provider, authorization server, workflow engine or general autonomous-agent framework.
