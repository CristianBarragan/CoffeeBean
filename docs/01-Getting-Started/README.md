# Getting Started

[Home](../../README.md) → [Documentation](../README.md) → **Getting Started**

Foundgine is an active architecture/proof project. The fastest way to understand it is to run the canonical Banking E2E.

## Prerequisites

- .NET 9 SDK
- Git

No external database is required for the canonical sample.

## Run the proof

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

The sample uses a real in-memory SQLite connection and proves:

```text
Domain
→ Metadata
→ Dynamic Planner
→ QueryPlan
→ ProviderPlan
→ SQL
→ database
→ Result
```

## Build and test

Use:

```bash
dotnet restore Foundgine.sln
dotnet build Foundgine.sln
dotnet test Foundgine.sln
```

The repository is under active development, so build/test status should be treated as the current source of truth.

## What to read next

- [Direction](../00-Direction/README.md)
- [Proof Milestones](../00-Direction/Milestones.md)
- [Architecture](../02-Architecture/README.md)
- [Banking Sample](../11-Samples/README.md)
- [Current Status](../CURRENT-STATUS.md)

## Historical material

The old GraphQL/Hot Chocolate sample and source-generator implementation are under `archive/`. They are useful for understanding project history but are not the current getting-started path.
