# Provider Execution IR Migration

Providers are now required to treat `ExecutionIR` as their canonical input.

The intended boundary is:

```text
Semantic IR
    ↓
Planner
    ↓
ExecutionPlan
    ↓
ExecutionIRCompiler
    ↓
ExecutionIR
    ↓
Provider compiler
    ↓
ProviderPlan
```

`ExecutionPlan` remains temporarily available as a compatibility adapter. It is not the provider contract.

## Current migrated providers

- `Foundgine.InMemory`
- `Foundgine.Sql`

The application engine now compiles `ExecutionIR` before entering the provider-plan cache/compiler boundary.

## Invariant

Providers must not receive semantic graphs or resolve semantic meaning.

They receive provider-neutral `ExecutionIR` and lower it into provider-specific plans.

The compatibility overloads will be removed once downstream consumers have migrated.
