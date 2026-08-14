# Foundgine

**Foundgine is a semantic execution layer for .NET.**

It takes a structured request, checks what that request means and what the caller is allowed to do, builds a provider-independent plan, and lets a provider execute that plan.

The simple idea is:

```text
Request
  ↓
Semantic meaning
  ↓
Authorization
  ↓
Execution plan
  ↓
Provider
  ↓
Result
```

A request can come from GraphQL, JSON, application code, or an AI system.

The request format is **not** the source of truth. Foundgine's semantic model is.

---

## Why Foundgine exists

A complex application often has several ways to ask for the same data:

```text
GraphQL ─┐
JSON ────┤
AI ──────┤
Code ────┘
          ↓
      Foundgine
          ↓
   SQL / InMemory / ...
```

Without a common layer, each entry point can end up with its own rules for:

- fields
- relationships
- filters
- authorization
- pagination
- mutations
- provider-specific execution

Foundgine puts those rules in one place.

The goal is simple:

> **Describe the operation once, authorize it once, plan it once, and let different providers execute it.**

---

## The six words to know

Foundgine deliberately uses a small vocabulary.

| Word | Simple meaning |
|---|---|
| **Model** | What the application exposes |
| **Request** | What the caller wants |
| **Authorization** | What the caller may do |
| **Plan** | What Foundgine decided should run |
| **Provider** | The system that does the physical work |
| **Result** | What came back, with execution evidence when available |

The code contains more detailed types and intermediate objects, but these six words are the main mental model.

---

## The layers

```text
                 GraphQL / JSON / AI / Code
                            │
                            ▼
                    Semantic request
                            │
                            ▼
                         Resolve
                            │
                            ▼
                       Authorize
                            │
                            ▼
                          Plan
                            │
                 ┌──────────┴──────────┐
                 ▼                     ▼
                SQL                InMemory
                 │                     │
                 └──────────┬──────────┘
                            ▼
                         Result
```

Each layer has one job.

### 1. Abstractions

Small shared contracts and IDs.

Project:

```text
src/Foundgine.Abstractions
```

### 2. Metadata

Describes the application model and the storage mapping.

Project:

```text
src/Foundgine.Metadata
```

### 3. Semantics

Defines entities, fields, relationships, requests, resolution, and authorization.

Project:

```text
src/Foundgine.Semantics
```

### 4. Planning

Turns an authorized request into a provider-independent plan.

Project:

```text
src/Foundgine.Planning
```

### 5. Execution

Defines provider execution contracts and turns provider rows into semantic results.

Project:

```text
src/Foundgine.Execution
```

### 6. Providers

Physical execution lives outside the semantic core.

Current providers:

```text
src/Foundgine.Sql
src/Foundgine.InMemory
```

SQL is the physical database path. InMemory is deliberately small and is mainly used to prove that the plan is not SQL-specific.

### 7. Input adapters

Input formats stay at the edge.

```text
src/Foundgine.Intent.Json
src/Foundgine.GraphQL.HotChocolate
src/Foundgine.GraphQL.HotChocolate.Mutations
```

### 8. AOT

Compile-time metadata support:

```text
src/Foundgine.Aot
src/Foundgine.Aot.Generator
```

---

## What Foundgine is not

Foundgine is not:

- an ORM;
- a GraphQL server;
- a database;
- an LLM framework;
- an agent framework;
- a workflow engine;
- an identity provider.

For normal object persistence, an ORM such as EF Core may still be the right tool.

Foundgine solves a different problem: **turning structured application intent into an authorized executable operation.**

---

## Quick start

### Requirements

- .NET 9 SDK
- Docker Engine + Docker Compose for PostgreSQL E2E tests
- Git
- Windows, Linux, or macOS

Check .NET:

```bash
dotnet --version
```

Check Docker:

```bash
docker version
docker compose version
```

### Build everything

From the repository root:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln --configuration Release
```

### Run the normal test suite

```bash
dotnet test Foundgine.sln --configuration Release
```

### Run PostgreSQL E2E tests

Start PostgreSQL 17:

```bash
docker compose -f docker-compose.postgres.yml up -d
```

Set the connection string.

PowerShell:

```powershell
$env:FOUNDGINE_POSTGRES_CONNECTION_STRING="Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine"
```

Bash:

```bash
export FOUNDGINE_POSTGRES_CONNECTION_STRING='Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine'
```

Run the E2E project:

```bash
dotnet test tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~Foundgine.E2E.Tests"
```

Stop PostgreSQL when finished:

```bash
docker compose -f docker-compose.postgres.yml down --volumes --remove-orphans
```

For a one-command Bash run, see `scripts/run-postgres-e2e.sh`.

For Windows PowerShell, see `scripts/run-postgres-e2e.ps1`.

---

## Set up each layer

The detailed guide is:

**[Layer setup guide](docs/LAYER-SETUP.md)**

It explains, in order:

1. Abstractions
2. Metadata
3. Semantics
4. Planning
5. Execution
6. SQL
7. InMemory
8. JSON
9. GraphQL
10. AOT
11. PostgreSQL E2E
12. PR checks

The guide also shows the smallest useful test for each layer.

---

## The best place to learn the code

Start here:

```text
tests/Foundgine.E2E.Tests
```

The Banking tests show the full read path:

```text
input
 → semantic request
 → resolution
 → authorization
 → plan
 → SQL
 → database
 → result
```

The PostgreSQL PostgreSQL E2E tests extend this to real PostgreSQL and complex mutation/query flows.

Then read:

- [Architecture](docs/ARCHITECTURE.md)
- [Layer setup](docs/LAYER-SETUP.md)
- [Testing](docs/TESTING.md)
- [PostgreSQL E2E](docs/POSTGRES-E2E.md)
- [Current status](docs/CURRENT-STATUS.md)

---

## PostgreSQL measurement gate

The repository has a deliberate measurement gate.

Before changing the PostgreSQL mutation compiler, run the real database tests and collect execution evidence.

The target matrix is:

```text
batch size: 1 / 10 / 50 / 500
depth:      1 / 2 / 3
```

The measurement should include:

```text
planning time
execution time

shared buffer hit/read/write
temporary read/write

WAL bytes

join type
sorts
materialization

estimated rows
actual rows
actual loops
```

The purpose is to optimize from PostgreSQL evidence rather than from guesses.

See:

- [PostgreSQL E2E](docs/POSTGRES-E2E.md)
- [PostgreSQL E2E measurement gate](docs/stage-48-MEASUREMENT-GATE-RECOMMENDATION.md)

---

## Pull requests

Every pull request to `main` runs:

```text
Build
  ↓
All tests
  ↓
PostgreSQL 17 E2E
```

The PostgreSQL job starts a clean PostgreSQL 17 container, runs the E2E tests, prints database diagnostics on failure, and removes the container afterwards.

Workflow:

```text
.github/workflows/build.yml
```

This means the PostgreSQL tests are optional on a developer machine but are a real CI check for pull requests.

---

## Project map

| Project | Job |
|---|---|
| `Foundgine.Abstractions` | Shared contracts and IDs |
| `Foundgine.Metadata` | Application and storage metadata |
| `Foundgine.Semantics` | Meaning, requests, resolution, authorization |
| `Foundgine.Planning` | Provider-independent plans |
| `Foundgine.Execution` | Execution contracts and result materialization |
| `Foundgine.Sql` | SQL provider |
| `Foundgine.InMemory` | Small non-SQL provider |
| `Foundgine.Intent.Json` | JSON input |
| `Foundgine.GraphQL.HotChocolate` | GraphQL input/schema |
| `Foundgine.GraphQL.HotChocolate.Mutations` | GraphQL mutations |
| `Foundgine.Aot` | AOT contracts |
| `Foundgine.Aot.Generator` | Generated metadata |

---

## Current proof

The active tests prove semantic modelling, resolution, authorization, provider-independent query and mutation planning, SQL/SQLite execution, a small InMemory provider, AOT metadata, JSON input, GraphQL adapters, relationship and aggregate operations, pagination, and PostgreSQL integration contracts.

The real PostgreSQL tests are the authoritative proof for the PostgreSQL execution path when `FOUNDGINE_POSTGRES_CONNECTION_STRING` is available.

The project does **not** claim universal provider support, autonomous-agent execution, workflow orchestration, or universal performance superiority.

---

## Documentation

Start with:

1. [Getting started](docs/GETTING-STARTED.md)
2. [Layer setup](docs/LAYER-SETUP.md)
3. [Architecture](docs/ARCHITECTURE.md)
4. [Testing](docs/TESTING.md)
5. [PostgreSQL E2E](docs/POSTGRES-E2E.md)
6. [Current status](docs/CURRENT-STATUS.md)
7. [Why Foundgine](docs/WHY-FOUNDGINE.md)
8. [Provider independence](docs/PROVIDER-INDEPENDENCE.md)
9. [Security](docs/SECURITY.md)
10. [Roadmap](docs/ROADMAP.md)

Historical design notes are kept under `docs/history`.
