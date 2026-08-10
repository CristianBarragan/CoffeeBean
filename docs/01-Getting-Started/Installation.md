# Installation

[Home](../../README.md) → [Getting Started](README.md) → **Installation**

## SDK

The active projects target:

```xml
<TargetFramework>net9.0</TargetFramework>
```

Install the .NET 9 SDK.

## Build

From the repository root:

```bash
dotnet restore
dotnet build Foundgine.sln
```

## Test

```bash
dotnet test Foundgine.sln
```

## Run the canonical sample

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

The sample creates an in-memory SQLite database and therefore does not require PostgreSQL, Docker or an external database.

## Important

`archive/` contains historical projects and should not be treated as part of the active solution architecture.
