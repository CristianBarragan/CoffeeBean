[Home](../../README.md) → [Documentation](../README.md) → **Runtime**

# Runtime

Foundgine runtime is the execution half of the platform.

Its principle is:

> **Runtime consumes explicit plans; it does not rediscover the application.**

Current active execution contracts include:

- `ExecutionContext`
- `ExecutionOptions`
- `ExecutionResult`
- `ExecutionRow`
- `ExecutionStatistics`
- `ProviderPlan`
- `ProviderNode`
- `IExecutionProvider`

The current Banking sample proves a real query path through these concepts.

## Target runtime lifecycle

```text
Intent
 ↓
Resolution
 ↓
Policy
 ↓
Plan
 ↓
Provider
 ↓
Execute
 ↓
Verify
 ↓
Evidence
```

Resolution, policy, verification and evidence are the next product layers; they should not be described as complete today.
