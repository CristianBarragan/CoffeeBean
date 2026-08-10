# Contracts

The active platform separates logical contracts from provider implementation.

Important contracts include:

```text
IMetadataProvider
IExecutionProvider
ExecutionContext
ExecutionResult
ExecutionRow
ProviderPlan
ProviderNode
```

Mutation execution has corresponding provider contracts.

## Contract rule

A contract should describe behavior without leaking an implementation.

For example, `IExecutionProvider` should not require callers to know that the implementation uses SQLite.

## Stability

The contracts are still evolving.

Do not treat them as a stable public NuGet API until a versioned release exists.
