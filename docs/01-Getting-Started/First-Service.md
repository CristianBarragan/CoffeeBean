[Home](../../README.md) → [Documentation](../README.md) → [Getting Started](README.md) → **First Service**

# First Proof

The canonical first service is the Banking console sample:

`samples/Foundgine.Samples.Banking`

## Domain

The sample models:

```text
Customer
   ↓
Account
   ↓
Transaction
```

## Run it

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

## What happens

1. Banking metadata describes the entities and joins.
2. `QueryIntent` describes the requested path.
3. `QueryPlanner` discovers the required join path from metadata.
4. `Foundgine.Builders.QueryPlan` represents the logical plan.
5. `SqlPlanCompiler` converts it to a provider plan.
6. `SqlExecutionProvider` executes it.
7. A real SQLite database returns rows.
8. The sample reads the resulting `ExecutionRow` occurrences.

The sample contains no GraphQL-specific request handling.

## Why this is the important first step

The project is not trying to prove an AI framework by mocking the lower execution layer.

It first proves that Foundgine can take an application domain and deterministically execute a plan against a real database.

The next milestone adds semantic resolution above this path.

## Target next step

Eventually the same domain should support:

```text
"Find Ada's last five transactions."
```

without replacing the existing execution path.

The intended extension is:

```text
Natural language
→ Intent
→ Resolution
→ Foundgine plan
→ existing execution path
→ Evidence
```

See [Proof Milestones](../00-Direction/Milestones.md).
