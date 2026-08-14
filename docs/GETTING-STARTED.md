# Getting started

Foundgine is easiest to understand by following one request through the layers.

## Requirements

- .NET 9 SDK
- Docker Engine and Docker Compose for PostgreSQL tests
- Git
- Windows, Linux, or macOS

Check:

```bash
dotnet --version
docker version
docker compose version
```

## Build

From the repository root:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln --configuration Release
```

## Run the normal tests

```bash
dotnet test Foundgine.sln --configuration Release
```

This does not require PostgreSQL.

## Follow one request

Start with:

```text
tests/Foundgine.E2E.Tests
```

The main path is:

```text
Input
  ↓
Semantic request
  ↓
Resolution
  ↓
Authorization
  ↓
Plan
  ↓
Provider
  ↓
Result
```

For the layer-by-layer setup, see [Layer setup](LAYER-SETUP.md).

## Run PostgreSQL 17

Start the test database:

```bash
docker compose -f docker-compose.postgres.yml up -d
```

Check it:

```bash
docker compose -f docker-compose.postgres.yml ps
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

Or use the helper script:

```bash
bash ./scripts/run-postgres-e2e.sh
```

PowerShell:

```powershell
.\scripts\run-postgres-e2e.ps1
```

Stop PostgreSQL:

```bash
docker compose -f docker-compose.postgres.yml down --volumes --remove-orphans
```

## Where to read next

1. [Layer setup](LAYER-SETUP.md)
2. [Architecture](ARCHITECTURE.md)
3. [Testing](TESTING.md)
4. [PostgreSQL E2E](POSTGRES-E2E.md)
5. [Current status](CURRENT-STATUS.md)
