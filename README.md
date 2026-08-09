# Foundgine

**Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

It is a domain-semantic and execution layer for AI-native applications.

Foundgine is deliberately **not** another LLM framework, RAG framework, MCP server implementation, ORM, workflow engine, or database.

```text
        Claude / ChatGPT / Cursor / other agents
                         │
                     MCP / API
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

## The problem

AI agents are good at reasoning about language, but an application is not language.

A real application has:

- entities
- identities
- relationships
- business operations
- authorization rules
- data sources
- side effects
- verification requirements

Today, developers commonly bridge that gap by writing a growing collection of custom tools.

Foundgine's thesis is:

> **The application already contains the domain knowledge. Compile and expose that knowledge as a constrained semantic execution surface instead of teaching every agent the application independently.**

## The core lifecycle

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

Not every request requires every stage. Reads can be simpler; mutations should normally pass through policy, preview/approval, execution and verification.

## What Foundgine owns

Foundgine owns the application-domain boundary:

- semantic entity and relationship metadata
- identity and entity resolution
- constrained query planning
- domain-action descriptors
- policy-aware planning
- execution plans
- execution-provider contracts
- verification
- evidence

## What Foundgine deliberately does not own

Use existing technologies for:

- LLM inference
- model hosting
- generic agent orchestration
- generic RAG
- vector databases
- MCP protocol implementation
- authentication infrastructure
- workflow engines
- message brokers
- ORM/database management
- hosting

Foundgine integrates with those technologies rather than recreating them.

## Current repository

The active solution currently contains:

```text
src/
├── Foundgine.Abstractions/
├── Foundgine.Foundation/
├── Foundgine.Metadata/
├── Foundgine.Diagnostics/
├── Foundgine.Builders/
├── Foundgine.Execution.Contracts/
├── Foundgine.Planning/
└── Foundgine.Providers/

samples/
└── Foundgine.Samples.Banking/

tests/
├── Foundgine.Tests/
├── Foundgine.Foundation.Tests/
├── Foundgine.Metadata.Tests/
├── Foundgine.Builders.Tests/
├── Foundgine.Diagnostics.Tests/
├── Foundgine.Execution.Contracts.Tests/
├── Foundgine.Planning.Tests/
└── Foundgine.Providers.Tests/
```

The active tree intentionally contains no GraphQL product project. Historical GraphQL/Graphgine work remains under `archive/`.

## Current E2E proof

The canonical sample is:

`samples/Foundgine.Samples.Banking`

It currently proves:

```text
Customer
   ↓
Account
   ↓
Transaction
```

through:

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

The sample uses real SQLite and does not depend on GraphQL, Hot Chocolate, or Graphgine.

Run it with:

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

This is the first proof, not the final product.

## The next proof

The immediate goal is to extend the Banking sample upward:

```text
"Find Ada's checking account."
        ↓
semantic resolution
        ↓
policy
        ↓
execution plan
        ↓
real database
        ↓
evidence
```

Then:

```text
"Refund Ada's last transaction."
        ↓
resolve
        ↓
authorize
        ↓
preview
        ↓
approve
        ↓
execute
        ↓
verify
        ↓
evidence
```

See [Proof Milestones](docs/00-Direction/Milestones.md).

## Architecture

Foundgine separates stable domain contracts from planning and execution:

```text
Foundgine.Abstractions
        ↓
Foundgine.Foundation
        ↓
Foundgine.Metadata
        ↓
Foundgine.Builders
        ↓
Foundgine.Planning
        ↓
Foundgine.Execution.Contracts
        ↓
Foundgine.Providers
```

These are architectural boundaries, not a claim that every provider capability is complete.

See:

- [Architecture](docs/02-Architecture/README.md)
- [Direction](docs/00-Direction/README.md)
- [Current Status](docs/CURRENT-STATUS.md)

## Important status

Foundgine is an active architecture and proof-of-concept project.

The lower execution path has a real Banking E2E proof. The AI-native layers are the next development phase:

- semantic domain model
- resolution
- actions
- policy
- preview/approval
- verification
- evidence
- MCP adapter

Do not interpret the repository as a production-ready AI agent platform yet.

## Why not another AI framework?

Because the goal is deliberately narrower.

```text
AI frameworks
    own reasoning/orchestration

Databases / ORMs
    own persistence

MCP
    owns agent-tool protocol

Workflow engines
    own durable workflows

Foundgine
    owns application-domain semantics
    and safe execution
```

That boundary is the product.

## Documentation

- [Direction](docs/00-Direction/README.md)
- [Proof Milestones](docs/00-Direction/Milestones.md)
- [Documentation Hub](docs/README.md)
- [Architecture](docs/02-Architecture/README.md)
- [Foundation](docs/03-Foundation/README.md)
- [Runtime](docs/04-Runtime/README.md)
- [AI Integration](docs/09-AI/README.md)
- [Banking Sample](docs/11-Samples/README.md)
- [Roadmap](docs/13-Reference/Roadmap.md)
- [Current Status](docs/CURRENT-STATUS.md)
- [Security](docs/SECURITY.md)
- [AI/LLM context](llms.txt)
- [Full AI context](llms-full.md)

## License

MIT
