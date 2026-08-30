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

An agent is also the clearest case for *why* a shared boundary matters: an agent with many tools can otherwise end up with as many independent execution and security surfaces, each only as correct as the tool that implements it. Routing every tool through the same semantic and authorization path — read or write — means that decision is made once, consistently, regardless of which tool or transport the request arrived through.

## What Foundgine is not

Foundgine is not an ORM replacement, database, GraphQL server, identity provider, authorization server, workflow engine or general autonomous-agent framework.

## Next

Read [Architecture](architecture/index.html) next.
