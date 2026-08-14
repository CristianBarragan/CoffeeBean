# Testing

Tests are the main proof of the current design.

## Run everything

```bash
dotnet test Foundgine.sln --configuration Release
```

This is the normal test run. It does not require PostgreSQL.

## Test projects

| Test project | What it checks |
|---|---|
| `Foundgine.Semantics.Tests` | Model, requests, resolution, authorization |
| `Foundgine.Planning.Tests` | Provider-independent plans |
| `Foundgine.InMemory.Tests` | Non-SQL provider |
| `Foundgine.Intent.Json.Tests` | JSON input and input limits |
| `Foundgine.GraphQL.HotChocolate.Tests` | GraphQL input and schema |
| `Foundgine.Aot.Tests` | Generated metadata |
| `Foundgine.E2E.Tests` | Full paths across layers |

## Useful commands

Semantic tests:

```bash
dotnet test tests/Foundgine.Semantics.Tests/Foundgine.Semantics.Tests.csproj
```

Planning tests:

```bash
dotnet test tests/Foundgine.Planning.Tests/Foundgine.Planning.Tests.csproj
```

InMemory tests:

```bash
dotnet test tests/Foundgine.InMemory.Tests/Foundgine.InMemory.Tests.csproj
```

JSON tests:

```bash
dotnet test tests/Foundgine.Intent.Json.Tests/Foundgine.Intent.Json.Tests.csproj
```

GraphQL tests:

```bash
dotnet test tests/Foundgine.GraphQL.HotChocolate.Tests/Foundgine.GraphQL.HotChocolate.Tests.csproj
```

AOT tests:

```bash
dotnet test tests/Foundgine.Aot.Tests/Foundgine.Aot.Tests.csproj
```

E2E:

```bash
dotnet test tests/Foundgine.E2E.Tests/Foundgine.E2E.Tests.csproj
```

PostgreSQL:

```bash
bash ./scripts/run-postgres-e2e.sh
```

PowerShell:

```powershell
.\scripts\run-postgres-e2e.ps1
```

## Test rule

Test the smallest layer that owns the rule.

Then add an E2E test when the rule crosses layers.

Example:

```text
New semantic rule
 → semantic unit test

New SQL translation
 → SQL/provider test

New full behavior
 → E2E test

New PostgreSQL behavior
 → real PostgreSQL E2E test
```

Do not weaken a test just to make an implementation pass.

If the contract is right, fix the implementation.

If the contract is wrong, change the contract and its tests together.
