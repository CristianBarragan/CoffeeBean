# Foundgine

> **Foundgine turns a .NET application's domain model into a safe, executable interface for AI agents.**

Foundgine is a **.NET application-domain semantic and execution platform** for AI-native applications.

It is not trying to be another LLM framework, RAG framework, ORM, workflow engine, database, or MCP implementation. The core problem Foundgine owns is the boundary between an application's **domain meaning** and **safe execution**.

```text
                AI / Application
                       │
                       ▼
                Semantic Intent
                       │
                       ▼
                ┌─────────────┐
                │  Foundgine  │
                │             │
                │ Resolution  │
                │ Policy      │
                │ Planning    │
                │ Execution   │
                │ Evidence    │
                └──────┬──────┘
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
      Structured    Domain      External
         data       actions       data
```

## The idea

A business application already knows:

- what entities exist;
- how they are identified;
- how entities relate;
- which fields are searchable;
- which operations are legal;
- what data may be accessed;
- how changes should be executed.

An AI model knows language and can propose intent, but it should not become the source of truth for those application rules.

Foundgine therefore aims to provide this boundary:

```text
Application domain
        ↓
Semantic model
        ↓
Structured intent
        ↓
Resolution
        ↓
Policy
        ↓
Execution plan
        ↓
Provider execution
        ↓
Verification / evidence
```

The AI is a **client of that boundary**, not the owner of it.

---

## Current status

Foundgine is an active architecture and proof-of-concept project.

The lower execution path is already proven against a real SQLite database:

```text
Metadata
   ↓
Dynamic QueryPlanner
   ↓
QueryPlan
   ↓
ProviderPlan
   ↓
SQL
   ↓
SQLite
   ↓
ExecutionRow
```

The semantic layer is also implemented far enough to prove:

```text
SemanticModel
   ↓
EntityResolver
   ↓
ResolvedReference
   ↓
ReadIntent
   ↓
ReadPlanner
   ↓
ResolvedReadPlan
```

A real end-to-end acceptance test also connects that resolved read to the existing query planner/provider pipeline for:

> **Find Ada Lovelace's last five transactions.**

A deeper semantic proof also exercises the five-entity composite domain and a repeated `Customer` occurrence. The remaining work is to turn the proven semantic-to-query handoff into a clean reusable runtime bridge and make collection-valued traversal explicit.

**This is not yet a production-ready autonomous-agent platform.**

---

# Documentation

### Direction

- [Product direction](docs/00-Direction/README.md)
- [Proof milestones](docs/00-Direction/Milestones.md)
- [Current status](docs/CURRENT-STATUS.md)

### Getting started

- [Installation](docs/01-Getting-Started/Installation.md)
- [First service](docs/01-Getting-Started/First-Service.md)
- [Configuration](docs/01-Getting-Started/Configuration.md)
- [FAQ](docs/01-Getting-Started/FAQ.md)

### Architecture

- [Architecture overview](docs/02-Architecture/README.md)
- [Layers](docs/02-Architecture/Layers.md)
- [Dependency graph](docs/02-Architecture/Dependency-Graph.md)
- [Principles](docs/02-Architecture/Principles.md)
- [Request pipeline](docs/02-Architecture/Request-Pipeline.md)
- [Vision](docs/02-Architecture/Vision.md)

### Core implementation

- [Foundation](docs/03-Foundation/README.md)
- [Metadata](docs/03-Foundation/Metadata.md)
- [Planning](docs/04-Runtime/README.md)
- [Execution](docs/04-Runtime/Execution.md)
- [Mutations](docs/04-Runtime/Mutations.md)
- [Semantic model](docs/09-AI/README.md)

### Proof

- [Banking sample](docs/11-Samples/README.md)
- [Testing](docs/12-Contributing/Testing.md)
- [Current benchmark plan](docs/10-Performance/Benchmarks.md)

### Reference

- [Glossary](docs/13-Reference/Glossary.md)
- [FAQ](docs/13-Reference/FAQ.md)
- [Roadmap](docs/13-Reference/Roadmap.md)
- [Changelog](docs/13-Reference/Changelog.md)
- [ADRs](docs/13-Reference/ADRs.md)

### AI context

- [`llms.txt`](llms.txt)
- [`llms-full.md`](llms-full.md)
- [`ai.seo.md`](ai.seo.md)

---

# Canonical proof

The Banking sample is intentionally small:

```text
Customer
   ↓
Account
   ↓
Transaction
```

It uses real metadata, a dynamic planner, provider compilation and a real SQLite connection.

Run:

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

The repository also contains E2E tests covering:

- linear traversal;
- branching traversal;
- ugly physical schemas;
- five-entity composites;
- repeated/self-joined entities;
- filtering, sorting and paging;
- create/update/delete mutations;
- semantic resolution;
- structured read intent;
- resolution → planning → real SQLite execution.

---

# What Foundgine is not

Foundgine does **not** attempt to replace:

- LLM providers;
- agent orchestration frameworks;
- MCP;
- EF Core;
- Dapper;
- databases;
- vector databases;
- workflow engines;
- message brokers.

Those technologies can sit around Foundgine.

For example:

```text
Claude / ChatGPT / Cursor
          ↓
         MCP
          ↓
Foundgine Semantic API
          ↓
Foundgine Runtime
          ↓
Application infrastructure
```

MCP is therefore an adapter, not the product.

---

# Product principle

The most important rule is:

> **The application is the source of truth.**

Foundgine should infer everything it can from the application's existing model and require explicit configuration only where semantics cannot be inferred.

For example, a future semantic mapping should ideally look closer to:

```csharp
new SemanticModelBuilder()
    .Entity<Customer>(customer =>
        customer.Search(x => x.Name, SearchStrategy.Fuzzy))
    .Build();
```

rather than requiring developers to describe every identity, field and relationship a second time.

That keeps semantic configuration focused on the things the application cannot safely infer:

- fuzzy/exact search;
- human-facing names;
- aliases;
- semantic descriptions;
- exposed actions;
- policy overrides.

---

# Development philosophy

Foundgine is intentionally being developed through **vertical proof milestones**.

The rule is:

> **Do not build an abstraction until a real scenario gives it a reason to exist.**

The next major proof is not another provider or another transport.

It is:

```text
Structured read intent
        ↓
Identity resolution
        ↓
Collection-aware traversal
        ↓
Reusable semantic → query bridge
        ↓
QueryPlan
        ↓
Real database
        ↓
Evidence
```

Only after that is solid should AI/MCP become a primary integration concern.
