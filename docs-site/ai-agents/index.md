# AI Agents with Foundgine

Foundgine gives an AI agent a controlled application capability surface without giving the model database authority.

## The problem it addresses

Without a shared boundary, each tool an agent can call is free to implement its own authorization, tenant filtering, and query logic:

```text
Agent
 ├── Tool A → its own auth / filtering / query logic
 ├── Tool B → its own auth / filtering / query logic
 └── Tool C → its own auth / filtering / query logic
```

An agent with dozens of tools can end up with dozens of independent execution and security surfaces. Foundgine routes every tool through one semantic and authorization boundary instead, so "what does this request mean, and is this caller allowed to make it" is answered the same way regardless of which tool the model called.

## Intended boundary

```text
AI agent
  ↓
capability discovery / structured intent
  ↓
Foundgine
  ├─ resolve
  ├─ validate
  ├─ authorize
  ├─ plan
  └─ execute
  ↓
provider
```

## Capability discovery is not authorization

Capability descriptions help a model construct valid intent. The server resolves and authorizes every actual request again.

## Security

Authentication, identity, tenant context and model orchestration remain host responsibilities. Foundgine enforces semantic authorization and preserves security constraints into planning/execution.

## Foundgine.AI

`Foundgine.AI` integrates with `Microsoft.Extensions.AI`, exposing Foundgine operations as model tools without hard-coding a model provider.

## What is outside the core guarantee

Foundgine is not a general autonomous-agent framework. Model selection, memory, orchestration, deployment and autonomous behavior belong to the surrounding application.

## Next

Read [Packages](../packages/index.html) next.
