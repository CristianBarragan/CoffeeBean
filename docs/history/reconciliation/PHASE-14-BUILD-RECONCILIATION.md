# Phase 14 — Build Reconciliation

## Scope

This phase reconciles the accumulated architecture against the actual repository tree.
The environment used for this pass does not contain the `dotnet` CLI, so compilation and
test execution cannot be claimed.

## Concrete finding fixed

A second `ExecutionReceipt` had been added under `Foundgine.Semantics` while the repository
already had the canonical `Foundgine.Execution.ExecutionReceipt` plus
`ExecutionReceiptFactory`.

That duplicate was removed.

The Phase 11 receipt tests were rewritten to exercise the existing canonical execution
receipt rather than introducing another receipt model.

## Canonical receipt

```text
Foundgine.Execution.ExecutionReceipt
Foundgine.Execution.ExecutionReceiptFactory
```

`Foundgine.Semantics` remains responsible for semantic versions and semantic contracts;
`Foundgine.Execution` remains responsible for execution evidence and receipts.

## Repository structure observations

- The solution contains the core and test projects needed for the architecture.
- Benchmark projects exist outside the main solution and should remain independently
  buildable unless the project policy changes.
- The repository currently has multiple explicit test package versions across test projects.
  This is a dependency-hygiene item, not something to silently normalize without a real
  build/test run.
- The MCP adapter references the Foundgine host layer and the JSON intent adapter rather
  than defining a second semantic execution engine.

## Build gate

A real CI environment must now run:

```text
dotnet restore Foundgine.sln
dotnet build Foundgine.sln --configuration Release --no-restore
dotnet test Foundgine.sln --configuration Release --no-build
```

Then independently validate benchmark projects and packaging.

## Required next fixes if CI reports them

1. Compile errors from the newly integrated MCP/mutation APIs.
2. Duplicate or conflicting public types revealed by compilation.
3. Package version conflicts.
4. Missing project references.
5. Analyzer/warning failures caused by `TreatWarningsAsErrors`.
6. Integration failures in MCP, GraphQL, SQL, AOT, and security tests.

## Freeze rule

Do not add durable workflows or more public agent features until the full solution passes
restore/build/test and the canonical receipt, capability, plan, approval, and authorization
paths are confirmed by integration tests.
