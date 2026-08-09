# Foundgine Direction

[Home](../../README.md) → **Direction**

Foundgine is being shaped around one narrow thesis:

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

Foundgine is **not** intended to become another LLM framework, RAG framework, MCP implementation, ORM, workflow engine, or hosting framework.

Those technologies can sit around Foundgine.

Foundgine owns the application-domain boundary between an AI agent and the business application.

## The product boundary

```text
        Claude / ChatGPT / Cursor / other agents
                         │
                    MCP / API / SDK
                         │
                         ▼
                ┌─────────────────┐
                │    Foundgine    │
                │                 │
                │ Domain semantics│
                │ Resolution      │
                │ Policy          │
                │ Planning        │
                │ Execution       │
                │ Verification    │
                │ Evidence        │
                └────────┬────────┘
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
          Structured   Domain     External
             data      actions     systems
              │          │
              ▼          ▼
          Database   Application services
```

The key idea is **semantic execution**, not AI inference.

## What Foundgine knows

Foundgine should understand facts that already exist in the application:

- entities
- identities
- fields
- relationships
- searchable properties
- domain actions
- action inputs
- mutation characteristics
- authorization requirements
- execution targets
- verification rules
- evidence produced by execution

The application remains the source of truth.

## What Foundgine does not own

Do not expand the core to own:

- LLM inference
- model hosting
- generic prompt orchestration
- generic RAG pipelines
- vector databases
- MCP protocol implementation
- authentication infrastructure
- workflow engines
- message brokers
- ORM functionality
- database servers
- transport-specific server frameworks

Integrations are welcome. Reimplementations are not.

## The runtime lifecycle

The target lifecycle is:

```text
DOMAIN MODEL
     ↓
SEMANTIC MODEL
     ↓
INTENT
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
     ↓
AI RESPONSE
```

Not every request requires every stage. A read may stop at execution and evidence. A mutation should normally include policy, preview/approval, execution and verification.

## Compile time versus runtime

The compiler should turn application source into a constrained semantic model.

```text
C# application
     │
     ▼
Foundgine compiler / generator
     │
     ├── entities
     ├── relationships
     ├── identities
     ├── searchable fields
     ├── actions
     ├── policies
     └── planner hints
     │
     ▼
Generated semantic descriptors
```

Runtime then performs dynamic reasoning over those descriptors:

```text
User / Agent intent
       ↓
Semantic resolution
       ↓
Plan
       ↓
Policy
       ↓
Execution
```

The plan is dynamic. The application's legal vocabulary is compiled.

## The first proof

The first product proof is deliberately small:

```text
Customer
   ↓
Account
   ↓
Transaction
```

The existing Banking sample already proves the lower execution path:

```text
Domain
  ↓
Metadata
  ↓
Dynamic Planner
  ↓
QueryPlan
  ↓
ProviderPlan
  ↓
SQL
  ↓
real SQLite database
  ↓
Result
```

The next milestones extend that same proof upward until an agent can safely operate the domain.

## What success looks like

A successful first release should make this possible without hand-writing an AI tool for every entity:

> "Find Ada Lovelace's checking account and show her last five transactions."

and:

> "Refund Ada's last transaction."

The first request should demonstrate resolution, planning, execution and evidence.

The second should demonstrate resolution, authorization, preview, approval, execution, verification and evidence.

## Related documents

- [Milestones](Milestones.md)
- [Architecture](../02-Architecture/README.md)
- [Current Status](../CURRENT-STATUS.md)
- [AI Integration](../09-AI/README.md)
- [Banking Sample](../11-Samples/README.md)
- [Roadmap](../13-Reference/Roadmap.md)
