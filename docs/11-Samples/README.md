[Home](../../README.md) → [Documentation](../README.md) → **Samples**

# Samples

## Foundgine.Samples.Banking

The canonical Foundgine proof domain is:

```text
Customer
   ↓
Account
   ↓
Transaction
```

The active sample is deliberately a console application with no GraphQL layer.

It currently proves:

```text
Domain
  ↓
Hand-written Metadata
  ↓
Dynamic QueryPlanner
  ↓
Foundgine.Builders.QueryPlan
  ↓
Foundgine.Providers.SqlPlanCompiler
  ↓
ProviderPlan
  ↓
SqlExecutionProvider
  ↓
real SQLite database
  ↓
ExecutionRow
```

Run:

```bash
dotnet run --project samples/Foundgine.Samples.Banking
```

## Why Banking?

The domain is small enough to understand but relational enough to prove:

- entity metadata
- identity
- relationships
- dynamic join discovery
- logical planning
- provider planning
- SQL execution
- real result materialization

## Next sample milestones

The same sample should grow vertically rather than spawning many disconnected demos:

### Read

```text
"Find Ada's last five transactions."
```

### Mutation

```text
"Refund Ada's last transaction."
```

The mutation should demonstrate:

```text
resolve
→ authorize
→ plan
→ preview
→ approve
→ execute
→ verify
→ evidence
```

## Historical sample

`archive/samples/Graphgine.Samples.Banking` contains the previous GraphQL/Hot Chocolate direction.

It is useful as historical material, but it is not the canonical proof for the current Foundgine direction.
