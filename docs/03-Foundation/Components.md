# Components

## Metadata

Describes entities, columns, relationships and joins.

## Builders

Contains provider-neutral logical plan structures such as:

```text
QueryPlan
QueryNode
CompositeNode
ProjectionNode
MutationPlan
MutationOperation
```

## Execution contracts

Contains:

```text
ProviderPlan
ProviderNode
ExecutionRow
ExecutionResult
ExecutionStatistics
```

## Diagnostics

Provides diagnostics support without owning planning or execution.

The projects are intentionally separate so that a provider does not become the architecture.
