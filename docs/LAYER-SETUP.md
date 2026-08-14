# Layer setup

This page explains how to work on Foundgine without having to understand the whole repository first.

The rule is simple:

> **Build and test one layer before moving to the next layer.**

## 0. Machine setup

Install:

- .NET 9 SDK
- Docker Engine + Compose
- Git

Verify:

```bash
dotnet --version
docker version
docker compose version
```

From the repository root:

```bash
dotnet restore Foundgine.sln
```

---

# 1. Abstractions

Path:

```text
src/Foundgine.Abstractions
tests/...
```

Purpose:

> Small types shared by the rest of the system.

Examples include IDs and stable contracts.

Rules:

- Keep this project small.
- Do not put SQL code here.
- Do not put GraphQL code here.
- Do not put provider behavior here.

Build:

```bash
dotnet build src/Foundgine.Abstractions/Foundgine.Abstractions.csproj
```

---

# 2. Metadata

Path:

```text
src/Foundgine.Metadata
```

Purpose:

> Describe the application model and how it maps to storage.

Think of metadata as the bridge between:

```text
Application meaning
        ↓
Storage shape
```

It should describe facts such as:

- entities;
- fields;
- relationships;
- tables;
- columns;
- keys.

Build:

```bash
dotnet build src/Foundgine.Metadata/Foundgine.Metadata.csproj
```

---

# 3. Semantics

Path:

```text
src/Foundgine.Semantics
```

Purpose:

> Define what the application means.

This is where the system understands:

- entities;
- fields;
- relationships;
- requests;
- filters;
- ordering;
- pagination;
- authorization;
- capabilities.

The important boundary is:

```text
GraphQL / JSON / AI
        ↓
semantic request
```

The input format should disappear after the adapter/resolution boundary.

Test:

```bash
dotnet test tests/Foundgine.Semantics.Tests/Foundgine.Semantics.Tests.csproj
```

---

# 4. Planning

Path:

```text
src/Foundgine.Planning
```

Purpose:

> Turn an authorized semantic request into a plan that does not know about SQL.

A plan should describe **what needs to happen**, not how PostgreSQL happens to do it.

Test:

```bash
dotnet test tests/Foundgine.Planning.Tests/Foundgine.Planning.Tests.csproj
```

If a new planner feature requires SQL types, stop and reconsider the boundary.

---

# 5. Execution

Path:

```text
src/Foundgine.Execution
```

Purpose:

> Define how a provider receives a plan and how results become semantic results.

The provider boundary is here:

```text
Plan
 ↓
Execution provider
 ↓
Rows
 ↓
Semantic result
```

Build:

```bash
dotnet build src/Foundgine.Execution/Foundgine.Execution.csproj
```

---

# 6. SQL provider

Path:

```text
src/Foundgine.Sql
```

Purpose:

> Turn the provider-independent plan into SQL and execute it.

This is where SQL-specific work belongs.

For SQLite/SQL pipeline tests:

```bash
dotnet test tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj \
  --filter "FullyQualifiedName~FoundgineSqlPipelineTests"
```

For PostgreSQL, continue to the PostgreSQL section below.

---

# 7. InMemory provider

Path:

```text
src/Foundgine.InMemory
```

Purpose:

> Execute the same logical plan without SQL.

It is intentionally small.

It is useful because it helps prove:

```text
Plan ≠ SQL
```

Tests:

```bash
dotnet test tests/Foundgine.InMemory.Tests/Foundgine.InMemory.Tests.csproj
```

Do not turn the InMemory provider into a second full database engine.

---

# 8. JSON input

Path:

```text
src/Foundgine.Intent.Json
```

Purpose:

> Convert JSON into a semantic request.

The JSON format is an input format. It should not leak into the semantic core.

Tests:

```bash
dotnet test tests/Foundgine.Intent.Json.Tests/Foundgine.Intent.Json.Tests.csproj
```

Security rule:

> Treat JSON as untrusted input.

Parser limits and semantic validation must happen before execution.

---

# 9. GraphQL

Paths:

```text
src/Foundgine.GraphQL.HotChocolate
src/Foundgine.GraphQL.HotChocolate.Mutations
```

Purpose:

> Convert GraphQL operations into Foundgine semantic requests.

GraphQL should not become the semantic model.

Test:

```bash
dotnet test tests/Foundgine.GraphQL.HotChocolate.Tests/Foundgine.GraphQL.HotChocolate.Tests.csproj
```

---

# 10. AOT

Paths:

```text
src/Foundgine.Aot
src/Foundgine.Aot.Generator
```

Purpose:

> Make stable model and metadata information available at compile time.

Build:

```bash
dotnet build src/Foundgine.Aot/Foundgine.Aot.csproj
```

Test:

```bash
dotnet test tests/Foundgine.Aot.Tests/Foundgine.Aot.Tests.csproj
```

AOT is an implementation detail around stable structure. It must not change semantic meaning.

---

# 11. Full local test

Once the individual layers are healthy:

```bash
dotnet test Foundgine.sln --configuration Release
```

The main end-to-end tests are:

```text
tests/Foundgine.E2E.Tests
```

Read these when you want to see the complete path:

```text
input
 → semantic request
 → resolve
 → authorize
 → plan
 → provider
 → result
```

---

# 12. PostgreSQL 17

The PostgreSQL setup is deliberately separate from the normal test suite.

Start it:

```bash
docker compose -f docker-compose.postgres.yml up -d
```

Check it:

```bash
docker compose -f docker-compose.postgres.yml ps
```

Check the server version:

```bash
docker compose -f docker-compose.postgres.yml exec -T postgres \
  psql -U foundgine -d foundgine_e2e -c 'select version();'
```

You should see PostgreSQL 17.

Set:

```text
FOUNDGINE_POSTGRES_CONNECTION_STRING
```

PowerShell:

```powershell
$env:FOUNDGINE_POSTGRES_CONNECTION_STRING="Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine"
```

Bash:

```bash
export FOUNDGINE_POSTGRES_CONNECTION_STRING='Host=localhost;Port=55432;Database=foundgine_e2e;Username=foundgine;Password=foundgine'
```

Run:

```bash
dotnet test tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~Postgres"
```

Then run all E2E tests:

```bash
dotnet test tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj \
  --configuration Release
```

Stop it:

```bash
docker compose -f docker-compose.postgres.yml down --volumes --remove-orphans
```

---

# 13. One-command PostgreSQL test

Bash:

```bash
bash ./scripts/run-postgres-e2e.sh
```

PowerShell:

```powershell
.\scripts\run-postgres-e2e.ps1
```

Both scripts:

```text
start PostgreSQL
 ↓
wait for health
 ↓
check PostgreSQL version
 ↓
set connection string
 ↓
run E2E tests
 ↓
remove the container
```

---

# 14. Pull requests

The PR workflow is:

```text
Build
 ↓
Normal tests
 ↓
PostgreSQL 17 E2E
```

File:

```text
.github/workflows/build.yml
```

The PostgreSQL job starts a clean PostgreSQL 17 container. It does not depend on a developer's local database.

This is the important difference:

```text
local dotnet test
    PostgreSQL tests may skip

PR check
    PostgreSQL tests run
```

---

# 15. Where a change belongs

Use this quick rule.

| Change | Layer |
|---|---|
| New ID/contract | Abstractions |
| New model/storage mapping | Metadata |
| New meaning/filter/relationship rule | Semantics |
| New logical operation | Planning |
| New result/execution contract | Execution |
| SQL syntax or PostgreSQL optimization | SQL |
| Non-SQL proof execution | InMemory |
| JSON parsing | JSON adapter |
| GraphQL parsing/schema | GraphQL adapter |
| Compile-time metadata | AOT |
| End-to-end behavior | E2E tests |

If a change seems to belong in two layers, first check whether the boundary is wrong.

---

# 16. Recommended development order

For a new feature:

```text
1. Contract
2. Semantic model
3. Semantic test
4. Planner
5. Plan test
6. Provider implementation
7. Provider test
8. E2E test
9. PostgreSQL test when physical SQL matters
10. Documentation
```

Do not start by changing the SQL compiler unless the semantic and plan layers already describe the feature correctly.

---

# 17. Before optimizing PostgreSQL

Do not change the compiler based only on generated SQL.

First run the real PostgreSQL test and measurement matrix.

Collect:

```text
planning time
execution time
buffer hits/reads/writes
temporary reads/writes
WAL bytes
join nodes
sort nodes
materialization
estimated rows
actual rows
actual loops
```

Then optimize the part PostgreSQL shows as expensive.
