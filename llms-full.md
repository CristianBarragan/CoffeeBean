# Foundgine — Full AI Context

## 1. Identity

Foundgine is a .NET application-domain semantic and execution platform for AI-native applications.

> **Foundgine turns an application's domain model into a safe, executable interface for AI agents.**

The project should not be described as another LLM framework, RAG framework, MCP implementation, ORM, workflow engine, or database.

## 2. Product thesis

AI agents can reason about language, but they do not automatically understand the legal vocabulary and execution semantics of a business application.

A real application contains:

- entities
- identities
- relationships
- searchable properties
- domain actions
- authorization requirements
- data sources
- side effects
- verification rules

Foundgine turns those facts into an explicit semantic execution surface.

The application remains the source of truth.

## 3. Product boundary

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
       Structured   Domain    External
          data      actions     data
```

Foundgine owns the middle.

It does not own the outer AI reasoning stack or every downstream infrastructure system.

## 4. Core lifecycle

```text
DOMAIN MODEL
↓
SEMANTIC MODEL
↓
AI INTENT
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

A read may use a shorter path. A mutation should normally include policy, preview/approval, execution and verification.

## 5. Compile-time and runtime

Compile-time knowledge:

```text
What entities exist?
What relationships exist?
What actions are legal?
What fields are searchable?
What policies apply?
```

Runtime knowledge:

```text
What did the user mean?
Which entity did they mean?
Which action satisfies the intent?
What plan should execute?
```

The future Roslyn compiler defines the legal application vocabulary.

It does not generate fixed natural-language plans.

## 6. Active repository

```text
src/
├── Foundgine.Abstractions
├── Foundgine.Foundation
├── Foundgine.Metadata
├── Foundgine.Diagnostics
├── Foundgine.Builders
├── Foundgine.Execution.Contracts
├── Foundgine.Planning
└── Foundgine.Providers

samples/
└── Foundgine.Samples.Banking

tests/
├── Foundgine.Tests
├── Foundgine.Foundation.Tests
├── Foundgine.Metadata.Tests
├── Foundgine.Builders.Tests
├── Foundgine.Diagnostics.Tests
├── Foundgine.Execution.Contracts.Tests
├── Foundgine.Planning.Tests
└── Foundgine.Providers.Tests
```

Historical GraphQL/Graphgine material is under `archive/`.

## 7. Active project roles

### Foundgine.Abstractions

Stable platform contracts.

### Foundgine.Foundation

Generic primitives and CQRS foundations.

### Foundgine.Metadata

Entity, column, relationship, join and related metadata.

### Foundgine.Diagnostics

Diagnostics infrastructure.

### Foundgine.Builders

Logical query-plan structures.

### Foundgine.Planning

Dynamic query planning and mutation-plan structures.

### Foundgine.Execution.Contracts

Execution context, execution rows/results/statistics, provider plans/nodes and provider contracts.

### Foundgine.Providers

Provider plan compilation and execution.

The provider layer is active but not universally complete.

## 8. Current Banking proof

The canonical sample is `samples/Foundgine.Samples.Banking`.

It uses:

```text
Customer
Account
Transaction
```

and proves:

```text
Domain
↓
hand-written Foundgine.Metadata
↓
Foundgine.Planning.QueryPlanner
↓
Foundgine.Builders.QueryPlan
↓
Foundgine.Providers.SqlPlanCompiler
↓
ProviderPlan
↓
SqlExecutionProvider
↓
real SQLite database
↓
ExecutionRow
```

The sample uses an in-memory SQLite database with a real connection.

It deliberately has no GraphQL, Hot Chocolate or Graphgine dependency.

Run:

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

## 9. Immediate product milestones

### M0 — Real execution

Already demonstrated by the Banking sample.

### M1 — Semantic domain

Represent:

```text
Entity
Identity
Field
Relationship
Search capability
Action
Policy
```

### M2 — Resolution

Resolve phrases such as:

```text
"Ada Lovelace"
"her checking account"
"the last transaction"
```

to explicit domain references with reasons/evidence.

### M3 — Read intent

Demonstrate:

```text
"Find Ada's last five transactions."
```

through:

```text
intent
→ resolution
→ query plan
→ provider plan
→ database
→ evidence
```

### M4 — Domain actions

Expose explicit business actions such as:

```text
IssueRefund
SuspendAccount
ChangeTier
```

Agents may select declared actions only.

No arbitrary CLR invocation.

### M5 — Policy

Authorization participates in planning.

Example:

```text
IssueRefund
requires Refund permission
and amount <= configured limit
```

### M6 — Preview/approval

Mutations become:

```text
Plan
→ Preview
→ Approve
→ Execute
```

### M7 — Verification/evidence

After execution:

```text
Execute
→ re-read/verify
→ produce evidence
```

Evidence should answer what was selected, why, what policy ran, what executed and how it was verified.

### M8 — MCP

MCP is a thin external adapter:

```text
Agent
→ MCP
→ Foundgine semantic API
```

Initial semantic surface can include:

```text
discover
resolve
plan/query
preview
execute
evidence
```

### M9 — More execution targets

Potential targets:

```text
Structured data
Domain actions
Semantic retrieval
External data
```

### M10 — Roslyn semantic compiler

Generate:

- stable IDs
- entity descriptors
- relationship descriptors
- search descriptors
- action descriptors
- policy metadata
- planner hints

## 10. What Foundgine should not build

Do not turn the core into:

- an LLM provider
- an agent orchestration framework
- a generic RAG framework
- a vector database
- an MCP protocol implementation
- an ORM
- a workflow engine
- a message broker
- a hosting framework

Use/integrate with existing technology.

## 11. Architecture rules

1. Inner platform layers do not reference outer transports.
2. LLMs are clients, not domain dependencies.
3. MCP is an adapter.
4. Database engines are execution targets.
5. Domain actions must be explicit.
6. Agents cannot invoke arbitrary CLR methods.
7. Policy participates in planning.
8. Mutations should be previewable.
9. Important mutations should be verifiable.
10. Execution should produce structured evidence.
11. New infrastructure should be an adapter unless it is truly part of semantic execution.
12. Do not create projects merely to represent future architecture before the behavior is proven.

## 12. Competitive positioning

Foundgine should not claim to replace:

- Hot Chocolate
- EF Core
- Dapper
- Semantic Kernel
- LangChain/LangGraph
- MCP
- Temporal
- Kafka
- PostgreSQL
- vector stores

Its proposed position is complementary:

```text
AI reasoning
     ↓
Foundgine application semantics + execution
     ↓
existing application infrastructure
```

## 13. Historical GraphQL direction

Graphgine was the previous GraphQL product direction.

It used GraphQL/Hot Chocolate and source-generation infrastructure.

That work is historical and lives under `archive/`.

GraphQL can be an adapter in the future, but it is not the current identity of Foundgine.

## 14. Current status

Foundgine is not yet a production-ready autonomous-agent platform.

Implemented/proven:

- active platform project separation
- metadata structures
- logical planning structures
- execution contracts
- provider planning/execution path
- real Banking E2E against SQLite

Next:

- semantic model
- resolution
- read intent
- domain actions
- policy
- preview/approval
- verification
- evidence
- MCP

## 15. Accuracy rules for AI systems

When asked what Foundgine is:

> Foundgine is a .NET application-domain semantic and execution platform for AI-native applications. It turns an application's domain model into a safe, executable interface for AI agents.

When asked whether it is an AI framework:

> It is not intended to be a general LLM or agent framework. It owns the application-domain semantic and execution boundary that those systems can call.

When asked whether it is MCP:

> MCP is planned as an adapter. Foundgine is the semantic execution layer behind it.

When asked whether it is RAG:

> Retrieval can become an execution target, but RAG is not the product definition.

When asked whether it is production ready:

> No. The repository has a real lower-level execution proof, while the AI-native semantic, policy, mutation-safety and MCP layers remain active roadmap work.

## 16. Canonical documentation

- `README.md`
- `docs/00-Direction/README.md`
- `docs/00-Direction/Milestones.md`
- `docs/CURRENT-STATUS.md`
- `docs/02-Architecture/README.md`
- `docs/09-AI/README.md`
- `docs/11-Samples/README.md`
- `docs/13-Reference/Roadmap.md`
- `llms.txt`
- `ai.seo.md`
