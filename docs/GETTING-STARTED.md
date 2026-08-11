# Getting started

## Requirements

- .NET 9 SDK
- Windows, Linux, or macOS

## Build and test

From the repository root:

```bash
dotnet restore
dotnet test
```

## The easiest path to understand

Start with the Banking end-to-end tests in `tests/Foundgine.E2E.Tests`.

They show the important path:

```text
Banking model
 → Semantic Request
 → Resolution
 → Authorization
 → Execution Plan
 → SQL
 → SQLite
```

## Where to look

- Domain contracts: `src/Foundgine.Abstractions`
- Semantic model: `src/Foundgine.Semantics`
- Planning: `src/Foundgine.Planning`
- SQL provider: `src/Foundgine.Sql`
- GraphQL adapter: `src/Foundgine.GraphQL.HotChocolate*`
- Acceptance tests: `tests/Foundgine.E2E.Tests`
