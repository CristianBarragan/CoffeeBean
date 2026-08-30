# AI Agents with Foundgine

Foundgine gives an AI agent a controlled application capability surface without giving the model database authority.

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
